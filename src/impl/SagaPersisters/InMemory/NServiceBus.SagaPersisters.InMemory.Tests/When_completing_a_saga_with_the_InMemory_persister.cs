﻿using System;
using NUnit.Framework;

namespace NServiceBus.SagaPersisters.InMemory.Tests
{
    using Persistence.InMemory.SagaPersister;
    using Saga;

    public class When_completing_a_saga_with_the_InMemory_persister
    {
        [Test]
        public void Should_delete_the_saga()
        {
            var inMemorySagaPersister = new InMemorySagaPersister() as ISagaPersister;
            var saga = new TestSaga { Id = Guid.NewGuid() };
            
            inMemorySagaPersister.Save(saga);
            Assert.NotNull(inMemorySagaPersister.Get<TestSaga>(saga.Id));
            inMemorySagaPersister.Complete(saga);
            Assert.Null(inMemorySagaPersister.Get<TestSaga>(saga.Id));
        }
    }
}
