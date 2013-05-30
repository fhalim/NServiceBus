using IHostConfiguration = EasyNetQ.IHostConfiguration;

namespace NServiceBus.Transports.RabbitMQ.Config
{
    using System.Collections.Generic;
    using System;

    public interface IConnectionConfiguration
    {
        ushort Port { get; }
        string VirtualHost { get; }
        string UserName { get; }
        string Password { get; }
        ushort RequestedHeartbeat { get; }
        ushort PrefetchCount { get; }
        IDictionary<string, string> ClientProperties { get; } 
        
        IEnumerable<IHostConfiguration> Hosts { get; }
        IEnumerable<IHostConfiguration> FailoverHosts { get; }
        TimeSpan RetryDelay { get; set; }
        TimeSpan ConnectionCreationTimeout { get; set; }
        bool UsePublisherConfirms { get; set; }
        TimeSpan MaxWaitTimeForConfirms { get; set; }
    }
}