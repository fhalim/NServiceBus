namespace NServiceBus.Transports.RabbitMQ.Tests.Routing
{
    using System;
    using EasyNetQ;
    using Moq;
    using NUnit.Framework;
    using RabbitMQ.Routing;
    using global::RabbitMQ.Client;

    [TestFixture]
    class ConventionalRoutingTopologyTests
    {
        ConventionalRoutingTopology topo;
        private readonly IHostConfiguration primaryHostConfig = new HostConfiguration {Host = "127.0.0.1", Port = 1234};
        private readonly IHostConfiguration failoverHostConfig = new HostConfiguration { Host = "127.0.0.1", Port = 1235, IsFailover = true };
        [SetUp]
        public void Setup()
        {
            topo = new ConventionalRoutingTopology();
        }
        [Test]
        public void ValidateExchangeCreation()
        {
            var model = new Mock<IModel>();
            topo.Publish(model.Object, typeof(String), primaryHostConfig, new TransportMessage(), null);
            model.Verify(m=>m.ExchangeDeclare("System:String", ExchangeType.Fanout, It.IsAny<bool>()));
            model.Verify(m=>m.ExchangeDeclare("System:Object", ExchangeType.Fanout, It.IsAny<bool>()));
            model.Verify(m=>m.ExchangeBind("System:Object", "System:String", String.Empty));
        }

        [Test]
        public void ValidateFailoverExchangeBinding()
        {
            var model = new Mock<IModel>();
            topo.Publish(model.Object, typeof(String), failoverHostConfig, new TransportMessage(), null);
            model.Verify(m => m.ExchangeDeclare("System:String", ExchangeType.Fanout, It.IsAny<bool>()));
            model.Verify(m => m.ExchangeDeclare("System:Object", ExchangeType.Fanout, It.IsAny<bool>()));
            model.Verify(m => m.ExchangeDeclare(ConventionalRoutingTopology.FailoverUpstreamExchangeName, ExchangeType.Fanout, It.IsAny<bool>()));
            model.Verify(m => m.ExchangeBind(ConventionalRoutingTopology.FailoverUpstreamExchangeName, "System:String", String.Empty));
            model.Verify(m => m.ExchangeBind("System:Object", "System:String", String.Empty));
        }

    }
}
