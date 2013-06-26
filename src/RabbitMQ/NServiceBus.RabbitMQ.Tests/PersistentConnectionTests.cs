namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Linq;
    using System.Net.Sockets;
    using Config;
    using EasyNetQ;
    using Logging.Loggers.NLogAdapter;
    using Moq;
    using NLog.Targets;
    using NUnit.Framework;
    using global::RabbitMQ.Client;

    [TestFixture]
    public class PersistentConnectionTests
    {
        Mock<IConnectionFactory> connFactoryMock;
        Mock<IConnection> firstConnection;
        Mock<IConnection> secondConnection;
        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            NLogConfigurator.Configure(new object[] { GetConsoleLoggingTarget() }, NLog.LogLevel.Trace.ToString());
        }
        [SetUp]
        public void Setup()
        {
            connFactoryMock = new Mock<IConnectionFactory>();
            firstConnection = new Mock<IConnection>();
            secondConnection = new Mock<IConnection>();
            connFactoryMock.SetupGet(f => f.CurrentHost).Returns(new HostConfiguration());
            connFactoryMock.SetupGet(f => f.Configuration).Returns(new ConnectionConfiguration());
            firstConnection.Setup(c => c.IsOpen).Returns(true);
            connFactoryMock.Setup(f => f.CreateConnection()).Returns(firstConnection.Object);
            connFactoryMock.SetupGet(f => f.Succeeded).Returns(true);
            
        }
        static ColoredConsoleTarget GetConsoleLoggingTarget()
        {
            return new ColoredConsoleTarget { Layout = "${longdate:universalTime=true} | ${logger:shortname=true} | ${threadid} | ${message} | ${exception:format=tostring}" };
        }

        [Test]
        public void It_should_return_same_connection_barring_errors()
        {
            using (var pc = new PersistentConnection(connFactoryMock.Object, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), true))
            {
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Once(),
                                       "Should have created a connection for the model");
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Once(), "Should reuse connection if not closed");
                connFactoryMock.Verify(f => f.Next(It.IsAny<Predicate<ConnectionFactoryInfo>>()), Times.Never(), "First connection is Ok, should not try to connect to another");
            }
        }

        [Test]
        public void It_should_try_to_connect_again_if_connection_was_dropped()
        {
            using (var pc = new PersistentConnection(connFactoryMock.Object, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), true))
            {
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Once(),
                                        "Should have created a connection for the model");
                firstConnection.Setup(c => c.IsOpen).Returns(false);
                secondConnection.Setup(c => c.IsOpen).Returns(true);
                connFactoryMock.Setup(f => f.CreateConnection()).Returns(secondConnection.Object);
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Exactly(2),
                                        "Should have gotten a new connection because of failure");
            }
        }

        [Test, Timeout(4000)]
        [ExpectedException(typeof (InvalidOperationException))]
        public void It_should_fail_within_time_window_if_no_servers_available()
        {
            connFactoryMock.Setup(m => m.CreateConnection()).Throws(new SocketException(12));
            connFactoryMock.Setup(m => m.Succeeded).Returns(false);
            using (var pc = new PersistentConnection(connFactoryMock.Object, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), true))
            {
                pc.CreateModel();
            }
        }
    }
}