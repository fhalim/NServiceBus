﻿namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using Routing;

    public class RabbitMqSubscriptionManager : IManageSubscriptions
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public string EndpointQueueName { get; set; }

        public IRoutingTopology RoutingTopology { get; set; }

        public void Subscribe(Type eventType, Address publisherAddress)
        {
            var connection = ConnectionManager.GetConnection(ConnectionPurpose.Administration);
            using (var channel = connection.CreateModel())
            {
                RoutingTopology.SetupSubscription(channel, eventType, connection.HostConfiguration, EndpointQueueName);
            }
        }

        public void Unsubscribe(Type eventType, Address publisherAddress)
        {
            using (var channel = ConnectionManager.GetConnection(ConnectionPurpose.Administration).CreateModel())
            {
                RoutingTopology.TeardownSubscription(channel, eventType, EndpointQueueName);
            }
        }
    }
}