namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using EasyNetQ;
    using System.Collections;
    using Logging;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;

    /// <summary>
    /// A connection that attempts to reconnect if the inner connection is closed.
    /// </summary>
    public class PersistentConnection : IPersistentConnection, IConnection
    {
        public PersistentConnection(IConnectionFactory connectionFactory, TimeSpan retryDelay, TimeSpan connectionCreationTimeout, bool failover)
        {
            connectingSemaphone = new SemaphoreSlim(1);
            this.connectionFactory = connectionFactory;
            this.retryDelay = retryDelay;
            this.connectionCreationTimeout = connectionCreationTimeout;
            if (failover)
            {
                connectionGuard = _ => false;
            }
            else
            {
                connectionGuard = h => h.HostConfiguration.IsFailover;
            }
            this.failover = failover;
            InitializeConnectionAsynchronously();
        }

        public event Action Connected;
        public event Action Disconnected;
        

        public IModel CreateModel()
        {
            if (!InitializeConnectionSynchronously())
            {
                throw new InvalidOperationException("Rabbit server is not connected.");
            }
            return connection.CreateModel();
        }

        bool InitializeConnectionSynchronously()
        {
            if (IsConnected)
                return true;
            InitializeConnectionAsynchronously();
            connectWaitHandle.Wait(connectionCreationTimeout);
            return IsConnected;
        }

        void InitializeConnectionAsynchronously()
        {
            bool iAmConnecting;
            try
            {
                iAmConnecting = connectingSemaphone.Wait(TimeSpan.Zero);
            }
            catch (Exception)
            {
                iAmConnecting = false;
            }

            if (iAmConnecting)
            {
                TryToConnect(null);
            }
            else
            {
                Logger.Debug("Waiting on original wait handle");
            }
        }

        public bool IsConnected
        {
            get { return connection != null && connection.IsOpen && !disposed; }
        }

        public void Close()
        {
            connection.ConnectionShutdown -= OnConnectionShutdown;
            connection.Close();
        }

        public void Close(int timeout)
        {
            connection.ConnectionShutdown -= OnConnectionShutdown;
            connection.Close(timeout);
        }


        void StartTryToConnect(object timer)
        {
            Task.Factory.StartNew(() =>
                {
                    try
                    {
                        TryToConnect(timer);
                    }
                    finally
                    {
                        FinishedConnecting();
                    }

                });
        }
        void TryToConnectLater()
        {
            var timer = new Timer(StartTryToConnect);
            timer.Change(Convert.ToInt32(retryDelay.TotalMilliseconds), Timeout.Infinite);
        }

        void FinishedConnecting()
        {
            connectingSemaphone.Release();
            connectWaitHandle.Reset();
        }

        void TryToConnect(object timer)
        {
            if (timer != null)
            {
                ((Timer) timer).Dispose();
            }

            Logger.Debug("Trying to connect");
            if (disposed)
            {
                FinishedConnecting();
                return;
            }

            connectionFactory.Reset(connectionGuard);
            do
            {
                var errored = true;
                try
                {
                    connection = connectionFactory.CreateConnection();
                    connectionFactory.Success();
                    FinishedConnecting();
                    errored = false;
                }
                catch (System.Net.Sockets.SocketException socketException)
                {
                    LogException(socketException);
                }
                catch (BrokerUnreachableException brokerUnreachableException)
                {
                    LogException(brokerUnreachableException);
                }
                Logger.InfoFormat("Could {2} connect to {0}:{1}. Failover: {3}", connectionFactory.CurrentHost.Host, connectionFactory.CurrentHost.Port, errored ? "not":String.Empty, failover);
            } while (!connectionFactory.Succeeded && connectionFactory.Next(connectionGuard));

            if (connectionFactory.Succeeded)
            {
                connection.ConnectionShutdown += OnConnectionShutdown;

                OnConnected();
                Logger.InfoFormat("Connected to RabbitMQ. Broker: '{0}', Port: {1}, VHost: '{2}', Failover:{3}",
                                  connectionFactory.CurrentHost.Host,
                                  connectionFactory.CurrentHost.Port,
                                  connectionFactory.Configuration.VirtualHost, failover);
            }
            else
            {
                Logger.ErrorFormat("Failed to connected to any Broker. Retrying in {0}", retryDelay);
                TryToConnectLater();
            }
        }

        void LogException(Exception exception)
        {
            Logger.ErrorFormat("Failed to connect to Broker: '{0}', Port: {1} VHost: '{2}'. , Failover:{4} " +
                               "ExceptionMessage: '{3}'",
                               connectionFactory.CurrentHost.Host,
                               connectionFactory.CurrentHost.Port,
                               connectionFactory.Configuration.VirtualHost,
                               exception.Message, failover);
        }

        void OnConnectionShutdown(IConnection _, ShutdownEventArgs reason)
        {
            if (disposed)
            {
                return;
            }
            OnDisconnected();

            Logger.InfoFormat("Disconnected from RabbitMQ Broker, reason: {0} , going to reconnect", reason);

            InitializeConnectionAsynchronously();
        }

        public void OnConnected()
        {
            Logger.Debug("OnConnected event fired");
            if (Connected != null)
            {
                Connected();
            }
        }

        public void OnDisconnected()
        {
            if (Disconnected != null)
            {
                Disconnected();
            }
        }


        public void Abort()
        {
            connection.Abort();
        }

        public void Abort(ushort reasonCode, string reasonText)
        {
            connection.Abort(reasonCode, reasonText);
        }

        public void Abort(int timeout)
        {
            connection.Abort(timeout);
        }

        public void Abort(ushort reasonCode, string reasonText, int timeout)
        {
            connection.Abort(reasonCode, reasonText, timeout);
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return connection.Endpoint; }
        }

        public IProtocol Protocol
        {
            get { return connection.Protocol; }
        }

        public ushort ChannelMax
        {
            get { return connection.ChannelMax; }
        }

        public uint FrameMax
        {
            get { return connection.FrameMax; }
        }

        public ushort Heartbeat
        {
            get { return connection.Heartbeat; }
        }

        public IDictionary ClientProperties
        {
            get { return connection.ClientProperties; }
        }

        public IDictionary ServerProperties
        {
            get { return connection.ServerProperties; }
        }

        public AmqpTcpEndpoint[] KnownHosts
        {
            get { return connection.KnownHosts; }
        }

        public ShutdownEventArgs CloseReason
        {
            get { return connection.CloseReason; }
        }

        public IHostConfiguration HostConfiguration { get { return connectionFactory.CurrentHost; } }

        public bool IsOpen
        {
            get { return connection.IsOpen; }
        }

        public bool AutoClose
        {
            get { return connection.AutoClose; }
            set { connection.AutoClose = value; }
        }

        public IList ShutdownReport
        {
            get { return connection.ShutdownReport; }
        }

        public event ConnectionShutdownEventHandler ConnectionShutdown
        {
            add { connection.ConnectionShutdown += value; }
            remove { connection.ConnectionShutdown -= value; }
        }

        public event CallbackExceptionEventHandler CallbackException
        {
            add { connection.CallbackException += value; }
            remove { connection.CallbackException -= value; }
        }

        public void Close(ushort reasonCode, string reasonText, int timeout)
        {
            connection.Close(reasonCode, reasonText, timeout);
        }

        public void Close(ushort reasonCode, string reasonText)
        {
            connection.Close(reasonCode, reasonText);
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
                if (connection == null)
                {
                    return;
                }

                try
                {
                    if (connection.IsOpen)
                    {
                        Close(10000);
                    }

                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failure when disposing RabbitMq connection", ex);
                }

                connection = null;
            }

            disposed = true;
        }

        ~PersistentConnection()
        {
            Dispose(false);
        }

        volatile bool disposed;
        volatile IConnection connection;
        readonly IConnectionFactory connectionFactory;
        readonly TimeSpan retryDelay;
        readonly TimeSpan connectionCreationTimeout;
        readonly Predicate<ConnectionFactoryInfo> connectionGuard;

        static readonly ILog Logger = LogManager.GetLogger(typeof (PersistentConnection));
        readonly bool failover;
        private readonly ManualResetEventSlim connectWaitHandle = new ManualResetEventSlim(false);
        private readonly SemaphoreSlim connectingSemaphone;
    }
}