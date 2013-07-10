namespace NServiceBus.Transports.RabbitMQ.Tests.ClusteringTests
{
    using System;
    using System.Linq;
    using System.Text;
    using FluentAssertions;
    using NUnit.Framework;
    using RabbitMQ.Routing;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;

    [TestFixture]
    [Category(TestCategory.Integration)]
    [Explicit("Long running test")]
    class when_sole_cluster_broker_dies_failover : ClusteredTestContext
    {
        TransportMessage messageReceivedWhenAllNodesUp;
        TransportMessage messageSentWhenAllNodesUp;
        TransportMessage messageSentAfterClusterFailure;
        const string upstreamQueueName = "federationupstreamqueue";
        const string failoverHostname = "localhost:5676";

        public when_sole_cluster_broker_dies_failover()
        {
            foreach (var rabbitNode in RabbitNodes.Skip(1).Take(2))
            {
                rabbitNode.Value.ShouldBeRunning = false;
            }

            foreach (var rabbitNode in RabbitNodes.Skip(3))
            {
                rabbitNode.Value.ShouldBeRunning = true;
            }
        }

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            // arrange
            SetupQueueAndSenderAndListener(String.Format("host=localhost:5673;failoverHost={0}", failoverHostname));
            SetupFailoverUpstreamstuff();

            // act
            messageReceivedWhenAllNodesUp = SendAndReceiveAMessage(out messageSentWhenAllNodesUp);
            StopNode(1);
            Console.WriteLine("After main server failure");
            SendMessage(out messageSentAfterClusterFailure);
            
        }

        static void SetupFailoverUpstreamstuff()
        {
            InConnection(failoverHostname, (connection, model) => model.ExchangeDeclare(ConventionalRoutingTopology.FailoverUpstreamExchangeName, ExchangeType.Fanout, true));
            InConnection(failoverHostname, (connection, model) => model.QueueDeclare(upstreamQueueName, true, false, false, null));
            InConnection(failoverHostname, (connection, model) => model.QueueBind(upstreamQueueName, ConventionalRoutingTopology.FailoverUpstreamExchangeName, String.Empty));
        }

        [Test]
        public void it_should_be_able_to_roundtrip_a_sent_message_when_all_nodes_are_up()
        {
            messageReceivedWhenAllNodesUp.Should().NotBeNull();
            messageReceivedWhenAllNodesUp.Id.Should().Be(messageSentWhenAllNodesUp.Id);
            
        }

        [Test]
        public void it_should_write_message_sent_on_failover_to_upstream_federation_queue()
        {
            InConnection(failoverHostname, (connection, model) =>
            {
                var firstMessage = GetFirstMessage(model, upstreamQueueName);
                firstMessage.Should().NotBeNull();
                firstMessage.Item2.MessageId.Should().Be(messageSentAfterClusterFailure.Id);
            });
        }

        static void InConnection(string hostname, Action<IConnection, IModel> callback)
        {
            InConnection(hostname, (conn, model) =>
            {
                callback(conn, model);
                return false;
            });
        }
        static T InConnection<T>(string hostname, Func<IConnection, IModel, T> callback)
        {
            var hostNameComponents = hostname.Split(':');
            var host = hostNameComponents[0];
            var port = Int32.Parse(hostNameComponents[1]);
            using (var connection = new ConnectionFactory { HostName = host, Port = port }.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    return callback(connection, model);
                }
            }
        }
        private static Tuple<string, IBasicProperties> GetFirstMessage(IModel model, string queueName)
        {
            var consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(queueName, true, consumer);
            object msg;
            var e = consumer.Queue.Dequeue(10000, out msg);
            var eventArgs = ((BasicDeliverEventArgs)msg);

            return e ? Tuple.Create(new string(Encoding.UTF8.GetChars(eventArgs.Body)), eventArgs.BasicProperties) : null;
        }

    }
}
