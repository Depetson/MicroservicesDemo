namespace Orders.Api.Data.Entities;

public class Order
{
    public Guid Id { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Created";
    public DateTime CreatedDate { get; set; }
}
