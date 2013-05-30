namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using Config;
    using EasyNetQ;
    using NUnit.Framework;

    class ConnectionFactoryWrapperTests
    {
        [Test]
        public void It_should_yield_default_host()
        {
            var connectionConfiguration = new ConnectionConfiguration();
            connectionConfiguration.PopulateHosts("localhost:1234");
            var cfw = new ConnectionFactoryWrapper(connectionConfiguration, new DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>() );
            Assert.AreEqual(1234, cfw.CurrentHost.Port);
            Assert.IsFalse(cfw.Next(_ => false));
        }
        [Test]
        public void It_should_yield_failoverhost_on_default_iteration()
        {
            var connectionConfiguration = new ConnectionConfiguration();
            connectionConfiguration.PopulateHosts("localhost:1234");
            connectionConfiguration.PopulateFailoverHosts("localhost:5678");
            var cfw = new ConnectionFactoryWrapper(connectionConfiguration, new DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>());
            Assert.AreEqual(1234, cfw.CurrentHost.Port);
            Assert.IsTrue(cfw.Next(_ => false));
            Assert.AreEqual(5678, cfw.CurrentHost.Port);
        }
        [Test]
        public void It_should_not_yield_failoverhost_when_told_not_to()
        {
            var connectionConfiguration = new ConnectionConfiguration();
            connectionConfiguration.PopulateHosts("localhost:1234");
            connectionConfiguration.PopulateFailoverHosts("localhost:5678");
            var cfw = new ConnectionFactoryWrapper(connectionConfiguration, new DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>());
            Assert.AreEqual(1234, cfw.CurrentHost.Port);
            Assert.IsFalse(cfw.Next(h => h.HostConfiguration.IsFailover));
        }

    }
}
