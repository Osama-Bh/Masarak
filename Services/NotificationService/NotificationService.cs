using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoWork.Services.NotificationService
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
        }

        public async Task SendTopicNotificationAsync(string topic, string title, string body, Dictionary<string, string>? data = null)
        {
            try
            {
                var message = new Message()
                {
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Topic = topic,
                };

                if (data != null)
                {
                    message.Data = data;
                }

                // Send a message to the devices subscribed to the provided topic.
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Successfully sent message to topic {Topic}. Response: {Response}", topic, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to topic {Topic}", topic);
                // Optionally throw or just log depending on whether you want notification failures to fail the main transaction
            }
        }
    }
}
