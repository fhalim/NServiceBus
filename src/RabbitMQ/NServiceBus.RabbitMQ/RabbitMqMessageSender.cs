namespace NServiceBus.Transports.RabbitMQ
{
    using Routing;

    public class RabbitMqMessageSender : ISendMessages
    {
        public IRoutingTopology RoutingTopology { get; set; }
        public void Send(TransportMessage message, Address address)
        {
            UnitOfWork.Add((channel, hostConfiguration) =>
                {
                    var properties = RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message,channel.CreateBasicProperties());
                    RoutingTopology.Send(channel, address, hostConfiguration, message, properties);
                });
        }

        public RabbitMqUnitOfWork UnitOfWork { get; set; }
    }
}