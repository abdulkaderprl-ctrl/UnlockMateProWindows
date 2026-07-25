using System;

namespace AdbEasyInstaller.Services
{
    public class NotificationService : INotificationService
    {
        public event EventHandler<NotificationEventArgs>? NotificationTriggered;

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            NotificationTriggered?.Invoke(this, new NotificationEventArgs
            {
                Title = title,
                Message = message,
                Type = type
            });
        }

        public void ShowSuccess(string title, string message) => ShowNotification(title, message, NotificationType.Success);
        public void ShowError(string title, string message) => ShowNotification(title, message, NotificationType.Error);
    }
}
