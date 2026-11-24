using System.Text.Json;
using WebPush;

namespace BlazingPizza.Application.Notifications;

public class WebPushNotificationSender : INotificationSender
{
	public async Task SendAsync(NotificationSubscription subscription, string message, string url)
	{
		// For a real application, generate your own
		var publicKey = "BLC8GOevpcpjQiLkO7JmVClQjycvTCYWm6Cq_a7wJZlstGTVZvwGFFHMYfXt6Njyvgx_GlXJeo5cSiZ1y4JOx1o";
		var privateKey = "OrubzSz3yWACscZXjFQrrtDwCKg-TGFuWhluQ2wLXDo";

		var pushSubscription = new PushSubscription(subscription.Url, subscription.P256dh, subscription.Auth);
		var vapidDetails = new VapidDetails("mailto:<someone@example.com>", publicKey, privateKey);
		var webPushClient = new WebPushClient();
		try
		{
			var payload = JsonSerializer.Serialize(new
			{
				message,
				url,
			});
			await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("Error sending push notification: " + ex.Message);
		}
	}
}
