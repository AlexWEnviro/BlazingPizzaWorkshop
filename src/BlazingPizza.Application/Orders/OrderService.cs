using System.ComponentModel.DataAnnotations;
using BlazingPizza.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BlazingPizza.Application;

public class OrderService : IOrderService
{
	private readonly PizzaStoreContext _db;
	private readonly INotificationSender _notifications;

	public OrderService(PizzaStoreContext db, INotificationSender notifications)
	{
		_db = db;
		_notifications = notifications;
	}

	public async Task<Result<List<OrderWithStatus>>> GetOrdersAsync(string? userId)
	{
		if (string.IsNullOrEmpty(userId))
		{
			return Result<List<OrderWithStatus>>.Unauthorized();
		}

		var orders = await OrdersWithDetails()
				.Where(o => o.UserId == userId)
				.OrderByDescending(o => o.CreatedTime)
				.ToListAsync();

		return Result<List<OrderWithStatus>>.Ok(orders.Select(OrderWithStatus.FromOrder).ToList());
	}

	public async Task<Result<OrderWithStatus>> GetOrderWithStatusAsync(int orderId, string? userId)
	{
		if (string.IsNullOrEmpty(userId))
		{
			return Result<OrderWithStatus>.Unauthorized();
		}

		var order = await OrdersWithDetails()
				.Where(o => o.OrderId == orderId && o.UserId == userId)
				.SingleOrDefaultAsync();

		if (order == null)
		{
			return Result<OrderWithStatus>.NotFound($"Order {orderId} was not found.");
		}

		return Result<OrderWithStatus>.Ok(OrderWithStatus.FromOrder(order));
	}

	public async Task<Result<int>> PlaceOrderAsync(Order order, string? userId)
	{
		if (string.IsNullOrEmpty(userId))
		{
			return Result<int>.Unauthorized();
		}

		// Web callers are validated by MVC model binding; other clients are not, so check here too
		var errors = Validate(order);
		if (errors.Count > 0)
		{
			return Result<int>.Invalid(errors);
		}

		order.CreatedTime = DateTime.Now;
		order.DeliveryLocation = new LatLong(51.5001, -0.1239);
		order.UserId = userId;

		// Enforce existence of Pizza.SpecialId and Topping.ToppingId
		// in the database - prevent the submitter from making up
		// new specials and toppings
		foreach (var pizza in order.Pizzas)
		{
			pizza.SpecialId = pizza.Special?.Id ?? 0;
			pizza.Special = null;

			foreach (var topping in pizza.Toppings)
			{
				topping.ToppingId = topping.Topping?.Id ?? 0;
				topping.Topping = null;
			}
		}

		_db.Orders.Attach(order);
		await _db.SaveChangesAsync();

		// In the background, send push notifications if possible
		var subscription = await _db.NotificationSubscriptions.Where(e => e.UserId == userId).SingleOrDefaultAsync();
		if (subscription != null)
		{
			_ = TrackAndSendNotificationsAsync(_notifications, order.OrderId, subscription);
		}

		return Result<int>.Ok(order.OrderId);
	}

	private IQueryable<Order> OrdersWithDetails() => _db.Orders
			.Include(o => o.DeliveryLocation)
			.Include(o => o.Pizzas).ThenInclude(p => p.Special)
			.Include(o => o.Pizzas).ThenInclude(p => p.Toppings).ThenInclude(t => t.Topping);

	private static List<string> Validate(Order order)
	{
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(order.DeliveryAddress, new ValidationContext(order.DeliveryAddress), results, validateAllProperties: true);

		var errors = results.Select(r => r.ErrorMessage ?? "Invalid delivery address.").ToList();
		if (order.Pizzas.Count == 0)
		{
			errors.Add("An order must contain at least one pizza.");
		}

		return errors;
	}

	// Static so the background task holds no reference to this scoped service or its DbContext
	private static async Task TrackAndSendNotificationsAsync(INotificationSender notifications, int orderId, NotificationSubscription subscription)
	{
		// In a realistic case, some other backend process would track
		// order delivery progress and send us notifications when it
		// changes. Since we don't have any such process here, fake it.
		var url = $"myorders/{orderId}";

		await Task.Delay(OrderWithStatus.PreparationDuration);
		await notifications.SendAsync(subscription, "Your order has been dispatched!", url);

		await Task.Delay(OrderWithStatus.DeliveryDuration);
		await notifications.SendAsync(subscription, "Your order is now delivered. Enjoy!", url);
	}
}
