﻿namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Transactions;
    using EasyNetQ;
    using global::RabbitMQ.Client;

    public class RabbitMqUnitOfWork
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        /// <summary>
        /// If set to true pulisher confirms will be used to make sure that messages are acked by the broker before considered to be published
        /// </summary>
        public bool UsePublisherConfirms { get; set; }

        /// <summary>
        /// The maximum time to wait for all publisher confirms to be received
        /// </summary>
        public TimeSpan MaxWaitTimeForConfirms { get; set; }

        public void Add(Action<IModel, IHostConfiguration> action)
        {
            var transaction = Transaction.Current;

            if (transaction == null)
            {
                ExecuteRabbitMqActions(new[] { action });

                return;
            }

            var transactionId = transaction.TransactionInformation.LocalIdentifier;

            if (!OutstandingOperations.ContainsKey(transactionId))
            {
                transaction.TransactionCompleted += ExecuteActionsAgainstRabbitMq;
                OutstandingOperations.Add(transactionId, new List<Action<IModel, IHostConfiguration>> { action });
                return;
            }

            OutstandingOperations[transactionId].Add(action);
        }

        void ExecuteActionsAgainstRabbitMq(object sender, TransactionEventArgs transactionEventArgs)
        {
            var transactionInfo = transactionEventArgs.Transaction.TransactionInformation;

            if (transactionInfo.Status != TransactionStatus.Committed)
            {
                OutstandingOperations.Clear();
                return;
            }

            var transactionId = transactionInfo.LocalIdentifier;

            if (!OutstandingOperations.ContainsKey(transactionId))
                return;

            var actions = OutstandingOperations[transactionId];

            if (!actions.Any())
                return;

            ExecuteRabbitMqActions(actions);

            OutstandingOperations.Clear();
        }

        void ExecuteRabbitMqActions(IEnumerable<Action<IModel, IHostConfiguration>> actions)
        {
            var connection = ConnectionManager.GetConnection(ConnectionPurpose.Publish);
            using (var channel = connection.CreateModel())
            {
                var host = connection.HostConfiguration;
                if (UsePublisherConfirms)
                {
                    channel.ConfirmSelect();
                }


                foreach (var action in actions)
                {
                    action(channel, host);
                }

                channel.WaitForConfirmsOrDie(MaxWaitTimeForConfirms);
            }
        }


        IDictionary<string, IList<Action<IModel, IHostConfiguration>>> OutstandingOperations
        {
            get
            {
                return outstandingOperations ?? (outstandingOperations = new Dictionary<string, IList<Action<IModel, IHostConfiguration>>>());
            }
        }


        //we use a dictionary to make sure that actions from other tx doesn't spill over if threads are getting reused by the hosting infrastrcture
        [ThreadStatic]
        static IDictionary<string, IList<Action<IModel, IHostConfiguration>>> outstandingOperations;
    }
}