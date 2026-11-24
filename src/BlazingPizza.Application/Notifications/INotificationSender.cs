namespace BlazingPizza.Application.Notifications;

public interface INotificationSender
{
	Task SendAsync(NotificationSubscription subscription, string message, string url);
}
