using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SubscriptionPlatform.Api1.Subscriptions.BackgroundServices;
using SubscriptionPlatform.Api1.Subscriptions.Data;
using SubscriptionPlatform.Api1.Subscriptions.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddNpgsqlDbContext<SubscriptionContext>("subscriptionsdb");

var rabbitMQConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? "amqp://guest:guest@localhost:5672/";

var factory = new ConnectionFactory()
{
    Uri = new Uri(rabbitMQConnectionString),
    DispatchConsumersAsync = true,
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
};

builder.Services.AddSingleton<IConnectionFactory>(factory);

builder.Services.AddScoped<IMessagePublisher, ResilientMessagePublisher>();
builder.Services.AddHostedService<PendingMessageProcessor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SubscriptionContext>();
    db.Database.Migrate();
    Console.WriteLine("✓ Migraciones aplicadas exitosamente");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Error en migraciones: {ex.Message}");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API 1 - Suscripciones v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine(" API 1 - SUSCRIPCIONES");
Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine($" RabbitMQ: {rabbitMQConnectionString}");
Console.WriteLine("✓ Resiliencia: ACTIVADA (PendingMessages + BackgroundService)");
Console.WriteLine("═════════════════════════════════════════════════════════════");

app.Run();
