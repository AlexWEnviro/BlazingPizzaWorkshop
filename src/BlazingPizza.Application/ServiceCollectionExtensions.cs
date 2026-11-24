using BlazingPizza.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace BlazingPizza.Application;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the application-layer services. The host must also register
	/// <see cref="PizzaStoreContext"/> with its chosen database provider.
	/// </summary>
	public static IServiceCollection AddPizzaApplication(this IServiceCollection services)
	{
		services.AddScoped<IOrderService, OrderService>();
		services.AddSingleton<INotificationSender, WebPushNotificationSender>();
		return services;
	}
}
