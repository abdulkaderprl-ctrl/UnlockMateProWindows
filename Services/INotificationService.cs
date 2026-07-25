using System;

namespace AdbEasyInstaller.Services
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
    }

    public interface INotificationService
    {
        event EventHandler<NotificationEventArgs>? NotificationTriggered;
        void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
        void ShowSuccess(string title, string message);
        void ShowError(string title, string message);
    }
}
