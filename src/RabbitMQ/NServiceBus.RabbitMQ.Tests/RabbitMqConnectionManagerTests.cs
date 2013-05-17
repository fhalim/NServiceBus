namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using Config;
    using EasyNetQ;
    using FluentAssertions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    class RabbitMqConnectionManagerTests
    {
        Mock<IConnectionFactory> connectionFactory;
        RabbitMqConnectionManager connectionManager;

        [SetUp]
        public void Setup()
        {
            connectionFactory = new Mock<IConnectionFactory>();
            connectionManager = new RabbitMqConnectionManager(connectionFactory.Object, new ConnectionConfiguration());
        }

        [Test]
        public void it_should_reuse_connections_if_no_failure()
        {
            var firstConnection = connectionManager.GetConnection(ConnectionPurpose.Publish);
            var secondConnection = connectionManager.GetConnection(ConnectionPurpose.Publish);
            firstConnection.Should().NotBeNull();
            firstConnection.Should().BeSameAs(secondConnection);
        }

        [Test]
        public void it_should_give_separate_connections_for_different_purposes()
        {
            var firstPublishConnection = connectionManager.GetConnection(ConnectionPurpose.Publish);
            var firstAdminConnection = connectionManager.GetConnection(ConnectionPurpose.Administration);
            var secondPublishConnection = connectionManager.GetConnection(ConnectionPurpose.Publish);
            var firstConsumeConnection = connectionManager.GetConnection(ConnectionPurpose.Consume);
            firstPublishConnection.Should().NotBeNull();
            firstAdminConnection.Should().NotBeNull();
            firstConsumeConnection.Should().NotBeNull();
            firstPublishConnection.Should().NotBeSameAs(firstAdminConnection);
            firstPublishConnection.Should().NotBeSameAs(firstConsumeConnection);
            firstAdminConnection.Should().NotBeSameAs(firstConsumeConnection);
        }
    }
}
