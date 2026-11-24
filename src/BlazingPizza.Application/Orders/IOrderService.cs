namespace BlazingPizza.Application;

/// <summary>
/// Order use cases, independent of any UI or HTTP concerns.
/// </summary>
public interface IOrderService
{
	Task<Result<List<OrderWithStatus>>> GetOrdersAsync(string? userId);

	Task<Result<OrderWithStatus>> GetOrderWithStatusAsync(int orderId, string? userId);

	/// <summary>Places the order for the given user and returns the new order id.</summary>
	Task<Result<int>> PlaceOrderAsync(Order order, string? userId);
}
