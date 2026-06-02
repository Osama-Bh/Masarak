using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;

namespace GoWork.Services.FirebaseNotificationSender
{
    public class FirebaseNotificationSender : IFirebaseNotificationSender
    {
        public async Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string>? data = null)
        {
            try
            {
                var message = new Message()
                {
                    Topic = topic,
                    Notification = new FirebaseAdmin.Messaging.Notification()
                    {
                        Title = title,
                        Body = body
                    }
                };

                if (data != null)
                {
                    message.Data = data;
                }

                await FirebaseMessaging.DefaultInstance.SendAsync(message);
            }
            catch
            {
                // Gracefully swallow errors as per business requirements to prevent API crashes.
            }
        }

        public async Task SendToTokensAsync(IEnumerable<string> tokens, string title, string body, Dictionary<string, string>? data = null)
        {
            try
            {
                var tokenList = tokens.Distinct().ToList();
                if (!tokenList.Any()) return;

                // FCM Multicast allows up to 500 tokens per request
                const int batchSize = 500;
                for (int i = 0; i < tokenList.Count; i += batchSize)
                {
                    var currentBatch = tokenList.Skip(i).Take(batchSize).ToList();

                    var multicastMessage = new MulticastMessage()
                    {
                        Tokens = currentBatch,
                        Notification = new FirebaseAdmin.Messaging.Notification()
                        {
                            Title = title,
                            Body = body
                        }
                    };

                    if (data != null)
                    {
                        multicastMessage.Data = data;
                    }

                    await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicastMessage);
                }
            }
            catch
            {
                // Gracefully swallow errors as per business requirements to prevent API crashes.
            }
        }
    }
}
