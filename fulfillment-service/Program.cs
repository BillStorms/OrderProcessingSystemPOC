using FulfillmentService.Worker;
using FulfillmentService.Worker.Services;
using Serilog;
using Serilog.Formatting.Json;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "FulfillmentService")
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting Fulfillment Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Register services
    builder.Services.AddSingleton<IShippingProvider, MockShippingProvider>();

    // Configure HTTP client for Order Service API
    builder.Services.AddHttpClient("OrderService", client =>
    {
        var orderServiceUrl = builder.Configuration["OrderService:BaseUrl"] ?? "http://localhost:5000";
        client.BaseAddress = new Uri(orderServiceUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // Register the background worker
    builder.Services.AddHostedService<FulfillmentWorker>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
