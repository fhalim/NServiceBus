namespace NServiceBus
{
    using System;
    using Notifications;

    /// <summary>
    /// Extension methods for notifications
    /// </summary>
    public static class BusExtensions
    {
        /// <summary>
        /// Sends the specified message via the <see cref="IBus"/> to an SMTP server for delivery.
        /// </summary>
        /// <param name="bus">The <see cref="IBus"/> that is sending the message.</param>
        /// <param name="message">The <see cref="MailMessage"/> to send.</param>
        public static void SendEmail(this IBus bus, MailMessage message)
        {
            if (ConfigureNotifications.NotificationsDisabled)
                throw new InvalidOperationException("Send email is not supported if notifications is disabled. Please remove Configure.DisableNotifications() from your config.");

            bus.Send(Configure.Instance.GetMasterNodeAddress().SubScope("Notifications"), new SendEmail
                                             {
                                                 Message = message
                                             });
        }
    }
}