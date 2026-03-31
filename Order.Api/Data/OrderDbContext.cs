using Microsoft.EntityFrameworkCore;
using Orders.Api.Data.Entities;

namespace Orders.Api.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {        
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedMessage>()
            .HasIndex(x => new { x.MessageId, x.ConsumerName })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
