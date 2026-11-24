using BlazingPizza.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazingPizza;

[Route("orders")]
[ApiController]
[Authorize]
public class OrdersController : Controller
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    private string? UserId => PizzaApiExtensions.GetUserId(HttpContext);

    [HttpGet]
    public async Task<ActionResult<List<OrderWithStatus>>> GetOrders()
        => ToActionResult(await _orders.GetOrdersAsync(UserId));

    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderWithStatus>> GetOrderWithStatus(int orderId)
        => ToActionResult(await _orders.GetOrderWithStatusAsync(orderId, UserId));

    [HttpPost]
    public async Task<ActionResult<int>> PlaceOrder(Order order)
        => ToActionResult(await _orders.PlaceOrderAsync(order, UserId));

    private ActionResult<T> ToActionResult<T>(Result<T> result) => result.Status switch
    {
        ResultStatus.Ok => new ActionResult<T>(result.Value!),
        ResultStatus.NotFound => NotFound(),
        ResultStatus.Unauthorized => Unauthorized(),
        _ => BadRequest(result.Errors),
    };
}
