using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using MassTransit;
using Notification.Consumer;
using StackExchange.Redis;
using static MassTransit.Logging.LogCategoryName;



var builder = Host.CreateApplicationBuilder(args);

var vaultUrl = builder.Configuration["AzureKeyVault:VaultUrl"];
if (!string.IsNullOrWhiteSpace(vaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(vaultUrl),
        new DefaultAzureCredential());
}

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    var transport = builder.Configuration["Messaging:Transport"];

    if (string.Equals(transport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
    {
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]!);
            cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"]!, "/", h =>
            {
                h.Username(builder.Configuration["RabbitMq:Username"]!);
                h.Password(builder.Configuration["RabbitMq:Password"]!);
            });

            cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            cfg.ConfigureEndpoints(context);
        });
    }
});

var host = builder.Build();
host.Run();