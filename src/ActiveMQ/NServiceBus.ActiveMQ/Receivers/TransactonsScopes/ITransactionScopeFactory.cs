namespace NServiceBus.Transports.ActiveMQ.Receivers.TransactonsScopes
{
    using System.Transactions;
    using Apache.NMS;
    using NServiceBus.Unicast.Transport.Transactional;
    using Unicast.Transport;

    public interface ITransactionScopeFactory
    {
        ITransactionScope CreateNewTransactionScope(TransactionSettings transactionSettings, ISession session);

        TransactionScope CreateTransactionScopeForAsyncMessage(TransactionSettings transactionSettings);
    }
}