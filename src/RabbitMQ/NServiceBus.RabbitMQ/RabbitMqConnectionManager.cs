namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Config;
    using EasyNetQ;
    using Logging;

    public class RabbitMqConnectionManager : IDisposable, IManageRabbitMqConnections
    {
        public RabbitMqConnectionManager(IConnectionFactory connectionFactory,IConnectionConfiguration connectionConfiguration)
        {
            this.connectionFactory = connectionFactory;
            this.connectionConfiguration = connectionConfiguration;
        }

        public IPersistentConnection GetConnection(ConnectionPurpose purpose)
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future

            lock (connectionFactory)
            {
                if (connectionFailed)
                    throw connectionFailedReason;

                return connections.ContainsKey(purpose) ? connections[purpose] : (connections[purpose] = new PersistentConnection(connectionFactory, connectionConfiguration.RetryDelay, connectionConfiguration.ConnectionCreationTimeout, purpose == ConnectionPurpose.Publish));
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
                    connections.Remove(connection.Key);
                }
            }

            disposed = true;
        }

        ~RabbitMqConnectionManager()
        {
            Dispose(false);
        }

        readonly IConnectionFactory connectionFactory;
        readonly IConnectionConfiguration connectionConfiguration;

        readonly IDictionary<ConnectionPurpose, PersistentConnection> connections = new ConcurrentDictionary<ConnectionPurpose, PersistentConnection>();
        bool connectionFailed;
        Exception connectionFailedReason;
        bool disposed;

        static readonly ILog Logger = LogManager.GetLogger(typeof(RabbitMqConnectionManager));
    }
}