using Azure.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Shared.Contracts;
using StackExchange.Redis;
using Entities = Orders.Api.Data.Entities;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.IdentityModel.Tokens;


static async Task<IResult> CreateOrder(
    OrderDbContext db,
    IPublishEndpoint publishEndpoint,
    IConnectionMultiplexer redis,
    int customerId,
    decimal totalAmount,
    Guid? messageId = null)
{
    if (customerId <= 0)
        return Results.BadRequest("CustomerId must be greater than 0.");

    if (totalAmount <= 0)
        return Results.BadRequest("TotalAmount must be greater than 0.");

    var order = new Entities.Order
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        TotalAmount = totalAmount,
        Status = "Created"
    };

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    if (messageId.HasValue)
    {
        await publishEndpoint.Publish(
            new OrderCreated(order.Id, order.CustomerId, order.TotalAmount),
            context => context.MessageId = messageId.Value);
    }
    else
    {
        await publishEndpoint.Publish(
            new OrderCreated(order.Id, order.CustomerId, order.TotalAmount));
    }

    var cache = redis.GetDatabase();
    await cache.StringSetAsync($"order:{order.Id}", $"Created:{order.TotalAmount}", TimeSpan.FromMinutes(5));

    return Results.Ok(new
    {
        order.Id,
        order.Status,
        order.TotalAmount
    });
}

var builder = WebApplication.CreateBuilder(args);

// Azure Key Vault (optional)
var vaultUrl = builder.Configuration["AzureKeyVault:VaultUrl"];
if (!string.IsNullOrWhiteSpace(vaultUrl))
{
    builder.Configuration.AddAzureKeyVault(new Uri(vaultUrl), new DefaultAzureCredential());
}

Console.WriteLine("SQL: " + builder.Configuration.GetConnectionString("SqlServer"));
Console.WriteLine("Redis: " + builder.Configuration["Redis:ConnectionString"]);
Console.WriteLine("Rabbit: " + builder.Configuration["RabbitMq:Host"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));

builder.Services.AddMassTransit(x =>
{
    var transport = builder.Configuration["Messaging:Transport"];

    if (string.Equals(transport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
    {
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]!);
        });
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"], "/", h =>
            {
                h.Username(builder.Configuration["RabbitMq:Username"]);
                h.Password(builder.Configuration["RabbitMq:Password"]);
            });
        });
    }
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/orders", async (
    CreateOrderRequest request,
    OrderDbContext db,
    IPublishEndpoint publishEndpoint,
    IConnectionMultiplexer redis) =>
{
    return await CreateOrder(db, publishEndpoint, redis, request.CustomerId, request.TotalAmount);
});

app.MapPost("/ordersWithFixedId", async (
    CreateOrderRequest request,
    OrderDbContext db,
    IPublishEndpoint publishEndpoint,
    IConnectionMultiplexer redis) =>
{

    return await CreateOrder(db, publishEndpoint, redis, request.CustomerId, request.TotalAmount, Guid.Parse("11111111-1111-1111-1111-111111111111"));
   
});

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    OrderDbContext db,
    IConnectionMultiplexer redis) =>
{
    var cache = redis.GetDatabase();
    var cached = await cache.StringGetAsync($"order:{id}");

    if (cached.HasValue)
    {
        return Results.Ok(new
        {
            Id = id,
            Source = "redis",
            Value = cached.ToString()
        });
    }

    var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == id);
    if (order is null)
        return Results.NotFound();

    return Results.Ok(new
    {
        order.Id,
        order.CustomerId,
        order.TotalAmount,
        order.Status,
        Source = "sql"
    });
});

app.Run();

public record CreateOrderRequest(int CustomerId, decimal TotalAmount);