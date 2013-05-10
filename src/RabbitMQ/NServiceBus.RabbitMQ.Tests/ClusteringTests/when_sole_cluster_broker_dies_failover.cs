namespace NServiceBus.Transports.RabbitMQ.Tests.ClusteringTests
{
    using System.Linq;
    using FluentAssertions;
    using NUnit.Framework;

    [TestFixture]
    [Category(TestCategory.Integration)]
    [Explicit("Long running test")]
    class when_sole_cluster_broker_dies_failover : ClusteredTestContext
    {
        TransportMessage messageReceivedWhenAllNodesUp;
        TransportMessage messageSentWhenAllNodesUp;

        public when_sole_cluster_broker_dies_failover()
        {
            foreach (var rabbitNode in RabbitNodes.Skip(1))
            {
                rabbitNode.Value.ShouldBeRunning = false;
            }
        }

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            // arrange
            var connectionString = GetConnectionString();
            SetupQueueAndSenderAndListener(connectionString);

            // act
            SendAndReceiveAMessage();
            messageReceivedWhenAllNodesUp = SendAndReceiveAMessage(out messageSentWhenAllNodesUp);
            StopNode(1);
        }

        [Test]
        public void it_should_be_able_to_roundtrip_a_message_when_all_nodes_are_up()
        {
            messageReceivedWhenAllNodesUp.Should().NotBeNull();
            messageReceivedWhenAllNodesUp.Id.Should().Be(messageSentWhenAllNodesUp.Id);
        }
    }
}
