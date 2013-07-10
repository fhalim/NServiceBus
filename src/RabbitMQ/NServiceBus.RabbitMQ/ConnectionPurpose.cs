namespace NServiceBus.Transports.RabbitMQ
{
    public enum ConnectionPurpose
    {
        Publish=1,
        Consume=2,
        Administration = 3
    }
}