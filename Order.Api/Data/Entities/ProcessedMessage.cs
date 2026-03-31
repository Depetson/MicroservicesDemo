namespace Orders.Api.Data.Entities;

//idempodency - database approach, for demo redis is used for idempotency
public class ProcessedMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = null!;
    public DateTime ProcessedAtUtc { get; set; }
    public string ConsumerName { get; set; } = null!;
}
