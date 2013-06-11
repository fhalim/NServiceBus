namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using Config;
    using EasyNetQ;

    public class RabbitMqConnectionManager : IDisposable, IManageRabbitMqConnections
    {
        public RabbitMqConnectionManager(Func<IConnectionFactory> connectionFactoryFactory,IConnectionConfiguration connectionConfiguration)
        {
            this.connectionFactoryFactory = connectionFactoryFactory;
            this.connectionConfiguration = connectionConfiguration;
        }

        public IPersistentConnection GetConnection(ConnectionPurpose purpose)
        {
            lock (connectionFactories)
            {
                return connections.GetOrAdd(purpose, p => new PersistentConnection(connectionFactories.GetOrAdd(purpose, p2 => connectionFactoryFactory()), connectionConfiguration.RetryDelay, connectionConfiguration.ConnectionCreationTimeout, p == ConnectionPurpose.Publish));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed resources.
                foreach (var connection in connections.Where(c => c.Value != null))
                {
                    connection.Value.Dispose();
                    PersistentConnection removedValue;
                    connections.TryRemove(connection.Key, out removedValue);
                }
            }

            disposed = true;
        }

        ~RabbitMqConnectionManager()
        {
            Dispose(false);
        }

        readonly Func<IConnectionFactory> connectionFactoryFactory;
        readonly IConnectionConfiguration connectionConfiguration;

        readonly ConcurrentDictionary<ConnectionPurpose, PersistentConnection> connections = new ConcurrentDictionary<ConnectionPurpose, PersistentConnection>();
        readonly ConcurrentDictionary<ConnectionPurpose, IConnectionFactory> connectionFactories = new ConcurrentDictionary<ConnectionPurpose, IConnectionFactory>();
        bool disposed;
    }
}