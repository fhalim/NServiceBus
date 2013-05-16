namespace NServiceBus.Transports.RabbitMQ.Tests.Routing
{
    using System;
    using System.Collections;
    using EasyNetQ;
    using Moq;
    using NUnit.Framework;
    using RabbitMQ.Routing;
    using Settings;
    using global::RabbitMQ.Client;

    [TestFixture]
    class ConventionalRoutingTopologyTests
    {
        ConventionalRoutingTopology topo;
        private readonly IHostConfiguration primaryHostConfig = new HostConfiguration {Host = "127.0.0.1", Port = 1234};
        private readonly IHostConfiguration failoverHostConfig = new HostConfiguration { Host = "127.0.0.1", Port = 1235, IsFailover = true };
        Mock<IModel> model;

        [SetUp]
        public void Setup()
        {
            topo = new ConventionalRoutingTopology();
            model = new Mock<IModel>();
            SettingsHolder.SetDefault("Endpoint.DurableMessages", true);
        }

        [Test]
        public void ValidateRoutingTopologyRecreatedOnFailover()
        {
            // Validate that we don't memoize route setups across servers
            topo.Publish(model.Object, typeof(String), primaryHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared("System:String", Times.Once());
            model.Verify(m => m.ExchangeBind("System:Object", "System:String", String.Empty), Times.Once());
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverDownstreamExchangeName);
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverUpstreamExchangeName, Times.Never());
            topo.Publish(model.Object, typeof(String), primaryHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared("System:String", Times.Once());
            topo.Publish(model.Object, typeof(String), failoverHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared("System:String", Times.Exactly(2));
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverDownstreamExchangeName, Times.Once());
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverUpstreamExchangeName, Times.Once());
        }

        [Test]
        public void ValidatePrimaryExchangeCreation()
        {
            topo.Publish(model.Object, typeof(String), primaryHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared("System:String");
            VerifyExchangeDeclared("System:Object");
            model.Verify(m=>m.ExchangeBind("System:Object", "System:String", String.Empty));
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverDownstreamExchangeName);
        }

        [Test]
        public void ValidatePrimaryExchangeBindingOnSend()
        {
            topo.Send(model.Object, new Address("mytarget", String.Empty), primaryHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverDownstreamExchangeName);
            model.Verify(m => m.QueueDeclare("mytarget", It.IsAny<bool>(), false, false, It.IsAny<IDictionary>()));
            model.Verify(m => m.ExchangeBind(ConventionalRoutingTopology.FailoverUpstreamExchangeName, "mytarget", String.Empty), Times.Never());
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverDownstreamExchangeName);
        }

        [Test]
        public void ValidateFailoverExchangeBindingOnPublish()
        {
            topo.Publish(model.Object, typeof(String), failoverHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared("System:String");
            VerifyExchangeDeclared("System:Object");
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverUpstreamExchangeName);
            model.Verify(m => m.ExchangeBind(ConventionalRoutingTopology.FailoverUpstreamExchangeName, "System:String", String.Empty));
            model.Verify(m => m.ExchangeBind("System:Object", "System:String", String.Empty));
        }

        [Test]
        public void ValidateFailoverExchangeBindingOnSend()
        {
            topo.Send(model.Object, new Address("mytarget", String.Empty), failoverHostConfig, new TransportMessage(), null);
            VerifyExchangeDeclared(ConventionalRoutingTopology.FailoverUpstreamExchangeName);
            model.Verify(m => m.QueueDeclare("mytarget", It.IsAny<bool>(), false, false, It.IsAny<IDictionary>()), Times.Never());
            model.Verify(m => m.ExchangeBind(ConventionalRoutingTopology.FailoverUpstreamExchangeName, "mytarget", String.Empty));
        }

        void VerifyExchangeDeclared(string exchangeName)
        {
            VerifyExchangeDeclared(exchangeName, Times.Once());
        }
        void VerifyExchangeDeclared(string exchangeName, Times times)
        {
            model.Verify(m=>m.ExchangeDeclare(exchangeName, ExchangeType.Fanout, It.IsAny<bool>()), times);
        }
    }
}
