namespace NServiceBus.Transports.RabbitMQ
{
    public interface IManageRabbitMqConnections
    {
        IPersistentConnection GetConnection(ConnectionPurpose purpose);
    }
}