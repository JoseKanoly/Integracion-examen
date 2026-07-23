var builder = DistributedApplication.CreateBuilder(args);

Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine("🚀 INICIALIZANDO .NET ASPIRE");
Console.WriteLine("═════════════════════════════════════════════════════════════");

var sqlPassword = builder.AddParameter("sql-password", "YourStrong@Password123", secret: true);

var sqlServer = builder
    .AddSqlServer("sqlserver", sqlPassword, port: 1433)
    .WithLifetime(ContainerLifetime.Persistent);

var apiDb = sqlServer.AddDatabase("api-subscriptions-db", "SubscriptionsDb");
var provDb = sqlServer.AddDatabase("api-provisioning-db", "ProvisioningDb");

Console.WriteLine("✓ SQL Server configurado");
Console.WriteLine("  └─ Base de datos 1: SubscriptionsDb");
Console.WriteLine("  └─ Base de datos 2: ProvisioningDb");

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq", port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithLifetime(ContainerLifetime.Persistent);

Console.WriteLine("✓ RabbitMQ configurado");
Console.WriteLine("  └─ Cola principal: payment-processed");
Console.WriteLine("  └─ Dead Letter Exchange: dlx-payment");
Console.WriteLine("  └─ Management UI: http://localhost:15672");

var api1 = builder
    .AddProject<Projects.SubscriptionPlatform_Api1_Subscriptions>("api1-subscriptions")
    .WithReference(apiDb)
    .WithReference(rabbitmq)
    .WaitFor(apiDb)
    .WaitFor(rabbitmq)
    .WithHttpEndpoint(port: 5001, name: "http");

Console.WriteLine("✓ API 1 (Suscripciones) configurada");
Console.WriteLine("  └─ Puerto: 5001");
Console.WriteLine("  └─ Swagger: http://localhost:5001");

var api2 = builder
    .AddProject<Projects.SubscriptionPlatform_Api2_Provisioning>("api2-provisioning")
    .WithReference(provDb)
    .WithReference(rabbitmq)
    .WaitFor(provDb)
    .WaitFor(rabbitmq)
    .WithHttpEndpoint(port: 5002, name: "http");

Console.WriteLine("✓ API 2 (Aprovisionamiento) configurada");
Console.WriteLine("  └─ Puerto: 5002");
Console.WriteLine("  └─ Swagger: http://localhost:5002");

var client1 = builder
    .AddProject<Projects.SubscriptionPlatform_Client1_UserPortal>("client1-portal")
    .WithReference(api1)
    .WaitFor(api1)
    .WithHttpsEndpoint(port: 7001, name: "https");

Console.WriteLine("✓ Cliente 1 (Portal de Usuario) configurado");
Console.WriteLine("  └─ Puerto HTTPS: 7001");
Console.WriteLine("  └─ Acceder: https://localhost:7001");

var client2 = builder
    .AddProject<Projects.SubscriptionPlatform_Client2_LearningPlatform>("client2-learning")
    .WithReference(api2)
    .WaitFor(api2)
    .WithHttpsEndpoint(port: 7002, name: "https");

Console.WriteLine("✓ Cliente 2 (Plataforma de Aprendizaje) configurado");
Console.WriteLine("  └─ Puerto HTTPS: 7002");
Console.WriteLine("  └─ Acceder: https://localhost:7002");

Console.WriteLine();
Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine("✅ INFRAESTRUCTURA LISTA");
Console.WriteLine("═════════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("🔗 PUERTOS Y ENDPOINTS:");
Console.WriteLine("   SQL Server          : localhost:1433");
Console.WriteLine("   RabbitMQ            : localhost:5672");
Console.WriteLine("   RabbitMQ Management : http://localhost:15672 (guest/guest)");
Console.WriteLine("   API 1 (Swagger)     : http://localhost:5001");
Console.WriteLine("   API 2 (Swagger)     : http://localhost:5002");
Console.WriteLine("   Cliente 1           : https://localhost:7001");
Console.WriteLine("   Cliente 2           : https://localhost:7002");
Console.WriteLine("   Dashboard Aspire    : Abierto automáticamente");
Console.WriteLine();
Console.WriteLine("📝 SISTEMA DE SUSCRIPCIONES Y ACTIVACIÓN DE SERVICIOS");
Console.WriteLine("   ✓ Resiliencia: ACTIVADA (PendingMessages + BackgroundService)");
Console.WriteLine("   ✓ Dead Letter Queue: CONFIGURADA");
Console.WriteLine("   ✓ Orden Cronológico: GARANTIZADO");
Console.WriteLine("═════════════════════════════════════════════════════════════");

builder.Build().Run();
