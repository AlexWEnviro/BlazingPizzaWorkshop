using BlazingPizza.Application.Notifications;
using BlazingPizza.Shared;

namespace BlazingPizza.Application.Tests;

public class FakeNotificationSender : INotificationSender
{
	public List<(NotificationSubscription Subscription, string Message, string Url)> SentNotifications { get; } = new();

	public Task SendAsync(NotificationSubscription subscription, string message, string url)
	{
		SentNotifications.Add((subscription, message, url));
		return Task.CompletedTask;
	}
}
