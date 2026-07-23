using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SubscriptionPlatform.Api2.Provisioning.Data;
using SubscriptionPlatform.Api2.Provisioning.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Cadena de conexión no configurada");

builder.Services.AddDbContext<ProvisioningContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddHostedService<PaymentEventConsumer>();

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
    var db = scope.ServiceProvider.GetRequiredService<ProvisioningContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API 2 - Aprovisionamiento v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine("🚀 API 2 - APROVISIONAMIENTO");
Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine($"📍 RabbitMQ: {rabbitMQConnectionString}");
Console.WriteLine("✓ Dead Letter Queue: ACTIVADA");
Console.WriteLine("✓ Validación de eventos: ACTIVADA");
Console.WriteLine("═════════════════════════════════════════════════════════════");

app.Run();
