using BlazingPizza.Shared;
using Microsoft.EntityFrameworkCore;

namespace BlazingPizza.Application.Tests;

public class OrderServiceTests
{
	private static PizzaStoreContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<PizzaStoreContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new PizzaStoreContext(options);
	}

	private static Order CreateValidOrder(string? userId = "user-1", int pizzaSpecialAndToppingId = 1) => new()
	{
		UserId = userId,
		DeliveryAddress = new Address
		{
			Name = "Alex Webster",
			Line1 = "1 Test Street",
			City = "Testville",
			Region = "Testshire",
			PostalCode = "1234",
		},
		Pizzas = new List<Pizza>
		{
			new()
			{
				Size = Pizza.DefaultSize,
				Special = new PizzaSpecial { Id = pizzaSpecialAndToppingId, Name = "Test Special", BasePrice = 10m },
				Toppings = new List<PizzaTopping>
				{
					new() { Topping = new Topping { Id = pizzaSpecialAndToppingId, Name = "Cheese", Price = 1.5m } },
				},
			},
		},
	};

	// --- GetOrdersAsync ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task GetOrdersAsync_WithNoUserId_ReturnsUnauthorized(string? userId)
	{
		var service = new OrderService(CreateContext(), new FakeNotificationSender());

		var result = await service.GetOrdersAsync(userId);

		Assert.Equal(ResultStatus.Unauthorized, result.Status);
	}

	[Fact]
	public async Task GetOrdersAsync_OnlyReturnsOrdersBelongingToTheGivenUser()
	{
		await using var db = CreateContext();
		var orderForUser = PersistPlacedOrder(db, CreateValidOrder("user-1", pizzaSpecialAndToppingId: 1));
		PersistPlacedOrder(db, CreateValidOrder("user-2", pizzaSpecialAndToppingId: 2));

		var service = new OrderService(db, new FakeNotificationSender());
		var result = await service.GetOrdersAsync("user-1");

		Assert.Equal(ResultStatus.Ok, result.Status);
		var order = Assert.Single(result.Value!);
		Assert.Equal(orderForUser.OrderId, order.Order.OrderId);
	}

	[Fact]
	public async Task GetOrdersAsync_ReturnsNewestOrderFirst()
	{
		await using var db = CreateContext();
		var older = CreateValidOrder(pizzaSpecialAndToppingId: 1);
		older.CreatedTime = DateTime.Now.AddDays(-1);
		var newer = CreateValidOrder(pizzaSpecialAndToppingId: 2);
		newer.CreatedTime = DateTime.Now;
		PersistPlacedOrder(db, older);
		PersistPlacedOrder(db, newer);

		var service = new OrderService(db, new FakeNotificationSender());
		var result = await service.GetOrdersAsync("user-1");

		Assert.Equal(new[] { newer.OrderId, older.OrderId }, result.Value!.Select(o => o.Order.OrderId));
	}

	// --- GetOrderWithStatusAsync ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task GetOrderWithStatusAsync_WithNoUserId_ReturnsUnauthorized(string? userId)
	{
		var service = new OrderService(CreateContext(), new FakeNotificationSender());

		var result = await service.GetOrderWithStatusAsync(1, userId);

		Assert.Equal(ResultStatus.Unauthorized, result.Status);
	}

	[Fact]
	public async Task GetOrderWithStatusAsync_UnknownOrderId_ReturnsNotFound()
	{
		var service = new OrderService(CreateContext(), new FakeNotificationSender());

		var result = await service.GetOrderWithStatusAsync(999, "user-1");

		Assert.Equal(ResultStatus.NotFound, result.Status);
	}

	[Fact]
	public async Task GetOrderWithStatusAsync_OrderBelongsToAnotherUser_ReturnsNotFound()
	{
		await using var db = CreateContext();
		var order = PersistPlacedOrder(db, CreateValidOrder("the-owner"));

		var service = new OrderService(db, new FakeNotificationSender());
		var result = await service.GetOrderWithStatusAsync(order.OrderId, "someone-else");

		Assert.Equal(ResultStatus.NotFound, result.Status);
	}

	[Fact]
	public async Task GetOrderWithStatusAsync_OwnOrder_ReturnsOk()
	{
		await using var db = CreateContext();
		var order = PersistPlacedOrder(db, CreateValidOrder("user-1"));

		var service = new OrderService(db, new FakeNotificationSender());
		var result = await service.GetOrderWithStatusAsync(order.OrderId, "user-1");

		Assert.Equal(ResultStatus.Ok, result.Status);
		Assert.Equal(order.OrderId, result.Value!.Order.OrderId);
	}

	// --- PlaceOrderAsync ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task PlaceOrderAsync_WithNoUserId_ReturnsUnauthorized(string? userId)
	{
		var service = new OrderService(CreateContext(), new FakeNotificationSender());

		var result = await service.PlaceOrderAsync(CreateValidOrder(), userId);

		Assert.Equal(ResultStatus.Unauthorized, result.Status);
	}

	[Fact]
	public async Task PlaceOrderAsync_WithNoPizzas_ReturnsInvalid()
	{
		var order = CreateValidOrder();
		order.Pizzas.Clear();
		var service = new OrderService(CreateContext(), new FakeNotificationSender());

		var result = await service.PlaceOrderAsync(order, "user-1");

		Assert.Equal(ResultStatus.Invalid, result.Status);
		Assert.Contains(result.Errors, e => e.Contains("at least one pizza"));
	}

	[Fact]
	public async Task PlaceOrderAsync_WithIncompleteDeliveryAddress_ReturnsInvalid()
	{
		var order = CreateValidOrder();
		order.DeliveryAddress.City = "";
		var service = new OrderService(CreateContext(), new FakeNotificationSender());

		var result = await service.PlaceOrderAsync(order, "user-1");

		Assert.Equal(ResultStatus.Invalid, result.Status);
		Assert.Contains(result.Errors, e => e.Contains("city"));
	}

	[Fact]
	public async Task PlaceOrderAsync_ValidOrder_PersistsItAndReturnsItsId()
	{
		await using var db = CreateContext();
		var service = new OrderService(db, new FakeNotificationSender());

		var result = await service.PlaceOrderAsync(CreateValidOrder(), "user-1");

		Assert.Equal(ResultStatus.Ok, result.Status);
		var persisted = await db.Orders.SingleAsync(o => o.OrderId == result.Value);
		Assert.Equal("user-1", persisted.UserId);
	}

	[Fact]
	public async Task PlaceOrderAsync_IgnoresClientSuppliedUserIdAndUsesTheAuthenticatedUser()
	{
		await using var db = CreateContext();
		var service = new OrderService(db, new FakeNotificationSender());
		var order = CreateValidOrder(userId: "someone-the-client-made-up");

		var result = await service.PlaceOrderAsync(order, "the-real-authenticated-user");

		var persisted = await db.Orders.SingleAsync(o => o.OrderId == result.Value);
		Assert.Equal("the-real-authenticated-user", persisted.UserId);
	}

	[Fact]
	public async Task PlaceOrderAsync_SetsCreatedTimeAndDeliveryLocationOnTheServer()
	{
		await using var db = CreateContext();
		var service = new OrderService(db, new FakeNotificationSender());
		var before = DateTime.Now;

		var result = await service.PlaceOrderAsync(CreateValidOrder(), "user-1");

		var persisted = await db.Orders.Include(o => o.DeliveryLocation).SingleAsync(o => o.OrderId == result.Value);
		Assert.True(persisted.CreatedTime >= before);
		Assert.NotNull(persisted.DeliveryLocation);
	}

	[Fact]
	public async Task PlaceOrderAsync_ResolvesSpecialAndToppingIdsFromTheNestedObjects_RatherThanTrustingClientSuppliedIds()
	{
		await using var db = CreateContext();
		var service = new OrderService(db, new FakeNotificationSender());
		var order = CreateValidOrder();
		order.Pizzas[0].SpecialId = 999; // a client could tamper with this directly; Special.Id should win
		order.Pizzas[0].Toppings[0].ToppingId = 999;

		var result = await service.PlaceOrderAsync(order, "user-1");

		var persisted = await db.Orders
			.Include(o => o.Pizzas).ThenInclude(p => p.Toppings)
			.SingleAsync(o => o.OrderId == result.Value);
		Assert.Equal(1, persisted.Pizzas[0].SpecialId);
		Assert.Equal(1, persisted.Pizzas[0].Toppings[0].ToppingId);
	}

	[Fact]
	public async Task PlaceOrderAsync_WithNoNotificationSubscriptionForUser_SucceedsWithoutSendingAnyNotification()
	{
		await using var db = CreateContext();
		var sender = new FakeNotificationSender();
		var service = new OrderService(db, sender);

		var result = await service.PlaceOrderAsync(CreateValidOrder(), "user-1");

		Assert.Equal(ResultStatus.Ok, result.Status);
		Assert.Empty(sender.SentNotifications);
	}

	[Fact]
	public async Task PlaceOrderAsync_ReturnsImmediately_EvenWhenAUserHasANotificationSubscription()
	{
		// Notifications are scheduled on a background task gated by multi-second/minute delays
		// (OrderWithStatus.PreparationDuration/DeliveryDuration) - placing the order must not
		// await that, or every checkout would hang for over a minute.
		await using var db = CreateContext();
		db.NotificationSubscriptions.Add(new NotificationSubscription { UserId = "user-1", Url = "https://example.com" });
		await db.SaveChangesAsync();
		var service = new OrderService(db, new FakeNotificationSender());

		var task = service.PlaceOrderAsync(CreateValidOrder(), "user-1");
		var completedInTime = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))) == task;

		Assert.True(completedInTime, "PlaceOrderAsync should not block on sending notifications.");
		Assert.Equal(ResultStatus.Ok, (await task).Status);
	}

	private static Order PersistPlacedOrder(PizzaStoreContext db, Order order)
	{
		order.DeliveryLocation = new LatLong(51.5001, -0.1239);
		db.Orders.Add(order);
		db.SaveChanges();
		return order;
	}
}
