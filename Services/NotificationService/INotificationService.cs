using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoWork.Services.NotificationService
{
    public interface INotificationService
    {
        Task SendTopicNotificationAsync(string topic, string title, string body, Dictionary<string, string>? data = null);
    }
}
