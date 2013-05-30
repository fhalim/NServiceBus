namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System.IO;
    using System.Linq;
    using EasyNetQ;
    using NUnit.Framework;

    [TestFixture]
    public class DefaultClusterHostSelectionStrategyTests
    {
        IClusterHostSelectionStrategy<int> defaultClusterHostSelectionStrategy;
        StringWriter writer;

        [SetUp]
        public void SetUp()
        {
            defaultClusterHostSelectionStrategy = new DefaultClusterHostSelectionStrategy<int>{0, 1, 2, 3};
            writer = new StringWriter();
        }
        [Test]
        public void It_should_honor_guard()
        {
            do
            {
                var item = defaultClusterHostSelectionStrategy.Current();
                writer.Write(item);
            } while (defaultClusterHostSelectionStrategy.Next(i => i > 2));
            Assert.AreEqual("012", writer.ToString());
        }

        [Test]
        public void Should_end_after_every_item_has_been_returned()
        {
            do
            {
                var item = defaultClusterHostSelectionStrategy.Current();
                writer.Write(item);
            } while (defaultClusterHostSelectionStrategy.Next(_ => false));

            Assert.AreEqual("0123", writer.ToString());
            Assert.IsFalse(defaultClusterHostSelectionStrategy.Succeeded);
        }

        [Test]
        public void Should_end_once_success_is_called()
        {
            var count = 0;
            do
            {
                var item = defaultClusterHostSelectionStrategy.Current();
                writer.Write(item);

                count++;
                if (count == 2) defaultClusterHostSelectionStrategy.Success();

            } while (defaultClusterHostSelectionStrategy.Next(_ => false));

            Assert.AreEqual("01", writer.ToString());
            Assert.IsTrue(defaultClusterHostSelectionStrategy.Succeeded);
        }

        [Test]
        public void Should_restart_from_next_item_and_then_try_all()
        {
            for (var i = 0; i < 10; i++)
            {
                var count = 0;
                defaultClusterHostSelectionStrategy.Reset();
                do
                {
                    var item = defaultClusterHostSelectionStrategy.Current();
                    writer.Write(item);

                    count++;
                    if (count == 3) defaultClusterHostSelectionStrategy.Success();

                } while (defaultClusterHostSelectionStrategy.Next(_ => false));
                writer.Write("_");
            }

            Assert.AreEqual("012_301_230_123_012_301_230_123_012_301_", writer.ToString());
        }

    }
}