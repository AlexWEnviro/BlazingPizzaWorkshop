namespace BlazingPizza.Shared;

public interface IRepository
{

	Task<List<Topping>> GetToppings();

	Task<List<PizzaSpecial>> GetSpecials();

	/// <summary>Orders placed by the current signed-in user. Scoping is enforced server-side.</summary>
	Task<List<OrderWithStatus>> GetOrdersAsync();

	/// <summary>An order belonging to the current signed-in user. Scoping is enforced server-side.</summary>
	Task<OrderWithStatus> GetOrderWithStatus(int orderId);

	Task<int> PlaceOrder(Order order);

	Task SubscribeToNotifications(NotificationSubscription subscription);

}
