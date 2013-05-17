namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using Config;
    using EasyNetQ;
    using Logging;
    using Moq;
    using NUnit.Framework;
    using global::RabbitMQ.Client;

    [TestFixture]
    public class PersistentConnectionTests
    {
        Mock<IConnectionFactory> connFactoryMock;
        Mock<IConnection> firstConnection;
        Mock<IConnection> secondConnection;

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

        [Test]
        public void It_should_return_same_connection_barring_errors()
        {
            using (var pc = new PersistentConnection(connFactoryMock.Object, TimeSpan.FromSeconds(1), true))
            {
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Once(), "Should have created a connection for the model");
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Once(), "Should reuse connection if not closed");
            }
        }

        [Test]
        public void It_should_try_to_connect_again_if_connection_was_dropped()
        {
            using (var pc = new PersistentConnection(connFactoryMock.Object, TimeSpan.FromSeconds(1), true))
            {
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Once(), "Should have created a connection for the model");
                firstConnection.Setup(c => c.IsOpen).Returns(false);
                secondConnection.Setup(c => c.IsOpen).Returns(true);
                connFactoryMock.Setup(f => f.CreateConnection()).Returns(secondConnection.Object);
                pc.CreateModel();
                connFactoryMock.Verify(f => f.CreateConnection(), Times.Exactly(2), "Should have gotten a new connection because of failure");
            }
        }

        [Test, Timeout(3000)]
        [ExpectedException(typeof(InvalidOperationException))]
        public void It_should_fail_within_time_window_if_no_servers_available()
        {
            firstConnection.Setup(c => c.IsOpen).Returns(false);
            using (var pc = new PersistentConnection(connFactoryMock.Object, TimeSpan.FromSeconds(1), true))
            {
                pc.CreateModel();
            }
        }

    }
}