using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoWork.Services.FirebaseNotificationSender
{
    public interface IFirebaseNotificationSender
    {
        Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string>? data = null);
        Task SendToTokensAsync(IEnumerable<string> tokens, string title, string body, Dictionary<string, string>? data = null);
    }
}
