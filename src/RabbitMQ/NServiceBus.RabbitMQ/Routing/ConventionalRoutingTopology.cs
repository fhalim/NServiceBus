namespace NServiceBus.Transports.RabbitMQ.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using EasyNetQ;
    using Settings;
    using global::RabbitMQ.Client;

    /// <summary>
    /// Implements the RabbitMQ routing topology as described at http://codebetter.com/drusellers/2011/05/08/brain-dump-conventional-routing-in-rabbitmq/
    /// take 4:
    /// <list type="bullet">
    /// <item><description>we generate an exchange for each queue so that we can do direct sends to the queue. it is bound as a fanout exchange</description></item>
    /// <item><description> for each message published we generate series of exchanges that go from concrete class to each of its subclass
    /// / interfaces these are linked together from most specific to least specific. This way if you subscribe to the base interface you get
    /// all the messages. or you can be more selective. all exchanges in this situation are bound as fanouts.</description></item>
    /// <item><description>the subscriber declares his own queue and his queue exchange –
    /// he then also declares/binds his exchange to each of the message type exchanges desired</description></item>
    /// <item><description> the publisher discovers all of the exchanges needed for a given message, binds them all up
    /// and then pushes the message into the most specific queue letting RabbitMQ do the fanout for him. (One publish, multiple receivers!)</description></item>
    /// <item><description>we generate an exchange for each queue so that we can do direct sends to the queue. it is bound as a fanout exchange</description></item>
    /// </list>
    /// </summary>
    public class ConventionalRoutingTopology : IRoutingTopology
    {
        public const string FailoverUpstreamExchangeName = "nsbfailoverupsteam";
        public const string FailoverDownstreamExchangeName = "nsbfailoverdownstream";

        public void SetupSubscription(IModel channel, Type type, IHostConfiguration hostConfiguration, string subscriberName)
        {
            CreateQueueAndExchangeForSubscriber(channel, subscriberName, hostConfiguration);
            if (type == typeof(IEvent))
            {
                // Make handlers for IEvent handle all events whether they extend IEvent or not
                type = typeof(object);
            }
            SetupTypeSubscriptions(channel, type, hostConfiguration);
            channel.ExchangeBind(subscriberName, ExchangeName(type), string.Empty);
        }

        void CreateQueueAndExchangeForSubscriber(IModel channel, string subscriberName, IHostConfiguration hostConfiguration)
        {
            if (endpointSubscriptionConfiguredSet.ContainsKey(Tuple.Create(subscriberName, hostConfiguration)))
            {
                return;
            }
            CreateExchange(channel, subscriberName);
            if (hostConfiguration.IsFailover)
            {
                SetupFailoverUpstreamSubscription(channel, subscriberName);
            }
            else
            {
                CreateQueue(channel, subscriberName);
                channel.QueueBind(subscriberName, subscriberName, string.Empty);
                SetupFailoverDownstreamSubscription(channel, subscriberName, subscriberName);
            }
            
            endpointSubscriptionConfiguredSet[Tuple.Create(subscriberName, hostConfiguration)] = null;
        }

        

        public void TeardownSubscription(IModel channel, Type type, string subscriberName)
        {
            try
            {
                channel.ExchangeUnbind(subscriberName, ExchangeName(type), string.Empty, null);
            }
            catch
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        public void Publish(IModel channel, Type type, IHostConfiguration hostConfiguration, TransportMessage message, IBasicProperties properties)
        {
            SetupTypeSubscriptions(channel, type, hostConfiguration);
            channel.BasicPublish(ExchangeName(type), String.Empty, true, false, properties, message.Body);
        }

        public void Send(IModel channel, Address address, IHostConfiguration hostConfiguration, TransportMessage message, IBasicProperties properties)
        {
            var subscriberName = address.Queue;
            CreateQueueAndExchangeForSubscriber(channel, subscriberName, hostConfiguration);
            channel.BasicPublish(subscriberName, String.Empty, true, false, properties, message.Body);
        }

        private readonly ConcurrentDictionary<Tuple<Type, IHostConfiguration>, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Tuple<Type, IHostConfiguration>, string>();
        private readonly ConcurrentDictionary<Tuple<string, IHostConfiguration>, string> endpointSubscriptionConfiguredSet = new ConcurrentDictionary<Tuple<string, IHostConfiguration>, string>();

        private static string ExchangeName(Type type)
        {
            return type.Namespace + ":" + type.Name;
        }

        private static void CreateQueue(IModel channel, string queueName)
        {
            var durable = SettingsHolder.Get<bool>("Endpoint.DurableMessages");
            try
            {
                channel.QueueDeclare(queueName, durable, false, false, null);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        private static void CreateExchange(IModel channel, string exchangeName)
        {
            try
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, true);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        private void SetupTypeSubscriptions(IModel channel, Type type, IHostConfiguration hostConfiguration)
        {
            if (IsTypeTopologyKnownConfigured(type, hostConfiguration))
            {
                return;
            }
            {
                var typeToProcess = type;
                CreateExchange(channel, ExchangeName(typeToProcess));
                if (hostConfiguration.IsFailover)
                {
                    SetupFailoverUpstreamSubscription(channel, type);
                }
                else
                {
                    SetupFailoverDownstreamSubscription(channel, type);
                }
                var baseType = typeToProcess.BaseType;
                while (baseType != null)
                {
                    CreateExchange(channel, ExchangeName(baseType));
                    channel.ExchangeBind(ExchangeName(baseType), ExchangeName(typeToProcess), string.Empty);
                    typeToProcess = baseType;
                    baseType = typeToProcess.BaseType;
                }
            }

            foreach (var exchangeName in type.GetInterfaces().Select(ExchangeName))
            {
                CreateExchange(channel, exchangeName);
                channel.ExchangeBind(exchangeName, ExchangeName(type), string.Empty);

            }
            MarkTypeConfigured(type, hostConfiguration);
        }

        static void SetupFailoverDownstreamSubscription(IModel channel, Type type)
        {
            SetupFailoverDownstreamSubscription(channel, ExchangeName(type), DefaultRoutingKeyConvention.GenerateRoutingKey(type));
        }

        static void SetupFailoverDownstreamSubscription(IModel channel, string exchangeName, string routingKey)
        {
            CreateExchange(channel, FailoverDownstreamExchangeName);
            channel.ExchangeBind(exchangeName, FailoverDownstreamExchangeName, routingKey);
        }

        static void SetupFailoverUpstreamSubscription(IModel channel, Type type)
        {
            var exchangeName = ExchangeName(type);
            SetupFailoverUpstreamSubscription(channel, exchangeName);
            
        }

        static void SetupFailoverUpstreamSubscription(IModel channel, string exchangeName)
        {
            CreateExchange(channel, FailoverUpstreamExchangeName);
            channel.ExchangeBind(FailoverUpstreamExchangeName, exchangeName, String.Empty);
        }

        private void MarkTypeConfigured(Type eventType, IHostConfiguration hostConfiguration)
        {
            typeTopologyConfiguredSet[Tuple.Create(eventType, hostConfiguration)] = null;
        }

        private bool IsTypeTopologyKnownConfigured(Type eventType, IHostConfiguration hostConfiguration)
        {
            return typeTopologyConfiguredSet.ContainsKey(Tuple.Create(eventType, hostConfiguration));
        }
    }
}