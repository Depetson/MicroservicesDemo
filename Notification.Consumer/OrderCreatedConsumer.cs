using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using StackExchange.Redis;

namespace Notification.Consumer;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IConnectionMultiplexer _redis;

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        var db = _redis.GetDatabase();

        var messageId = context.MessageId?.ToString() ?? "fallback";
        var key = $"processed:OrderCretedConsumer:{messageId}";


        //for demo Redis is used for idempotency
        var alreadyProcessed = await db.StringGetAsync(key);
        if (alreadyProcessed.HasValue)
        {
            _logger.LogInformation("Already processed message with ID: {MessageId}", messageId);
            return;
        }

        _logger.LogInformation(
            "OrderCreated received. OrderId={OrderId}, CustomerId={CustomerId}, Total={Total}",
            message.OrderId,
            message.CustomerId,
            message.TotalAmount);

        // retry demo
        //  throw new Exception("Failure - retry");

        await Task.Delay(500);
        await db.StringSetAsync(key, "1", TimeSpan.FromHours(24));

        _logger.LogInformation("Message processed successfully. MessageId = {MessageId}", messageId);
    }
}