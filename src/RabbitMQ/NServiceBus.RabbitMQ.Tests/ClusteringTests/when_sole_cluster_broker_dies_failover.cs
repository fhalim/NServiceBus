namespace NServiceBus.Transports.RabbitMQ.Tests.ClusteringTests
{
    using System;
    using System.Linq;
    using System.Text;
    using FluentAssertions;
    using NUnit.Framework;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;

    [TestFixture]
    [Category(TestCategory.Integration)]
    [Explicit("Long running test")]
    class when_sole_cluster_broker_dies_failover : ClusteredTestContext
    {
        TransportMessage messageReceivedWhenAllNodesUp;
        TransportMessage messageSentWhenAllNodesUp;
        TransportMessage messageSentAfterClusterFailure;

        public when_sole_cluster_broker_dies_failover()
        {
            foreach (var rabbitNode in RabbitNodes.Skip(1).Take(2))
            {
                rabbitNode.Value.ShouldBeRunning = false;
            }

            foreach (var rabbitNode in RabbitNodes.Skip(3))
            {
                rabbitNode.Value.ShouldBeRunning = true;
            }
        }

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            // arrange
            SetupQueueAndSenderAndListener("host=localhost:5673;failoverHost=localhost:5676");

            // act
            messageReceivedWhenAllNodesUp = SendAndReceiveAMessage(out messageSentWhenAllNodesUp);
            StopNode(1);
            SendMessage(out messageSentAfterClusterFailure);
        }

        [Test]
        public void it_should_be_able_to_roundtrip_a_message_when_all_nodes_are_up()
        {
            messageReceivedWhenAllNodesUp.Should().NotBeNull();
            messageReceivedWhenAllNodesUp.Id.Should().Be(messageSentWhenAllNodesUp.Id);
            InConnection("localhost:5676", (connection, model) =>
                { GetFirstMessage(model, QueueName).Should().NotBeNull(); });

        }

        static void InConnection(string hostname, Action<IConnection, IModel> callback)
        {
            InConnection(hostname, (conn, model) =>
            {
                callback(conn, model);
                return false;
            });
        }
        static T InConnection<T>(string hostname, Func<IConnection, IModel, T> callback)
        {
            var host = hostname.Split(':')[0];
            var port = Int32.Parse(hostname.Split(':')[1]);
            using (var connection = new ConnectionFactory { HostName = host, Port = port }.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    return callback(connection, model);
                }
            }
        }
        private static Tuple<string, IBasicProperties> GetFirstMessage(IModel model, string queueName)
        {
            var consumer = new QueueingBasicConsumer(model);
            model.BasicConsume(queueName, true, consumer);
            object msg;
            var e = consumer.Queue.Dequeue(10000, out msg);
            var eventArgs = ((BasicDeliverEventArgs)msg);

            return e ? Tuple.Create(new string(Encoding.UTF8.GetChars(eventArgs.Body)), eventArgs.BasicProperties) : null;
        }

    }
}
