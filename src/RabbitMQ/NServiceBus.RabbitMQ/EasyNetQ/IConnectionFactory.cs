namespace EasyNetQ
{
    
    using System;
    using RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Config;

    public interface IConnectionFactory
    {
        IConnection CreateConnection();
        IConnectionConfiguration Configuration { get; }
        IHostConfiguration CurrentHost { get; }
        bool Next(Predicate<ConnectionFactoryInfo> guard);
        void Success();
        void Reset(Predicate<ConnectionFactoryInfo> guard);
        bool Succeeded { get; }
    }
}