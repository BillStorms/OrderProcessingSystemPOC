
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using OrderService.Service.Interfaces;
using OrderService.Service.Services;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.Kafka;
using OrderService.Api.Extensions;
using Serilog;
using Serilog.Formatting.Json;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "OrderService")
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting Order Service");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Database configuration (uncomment to use SQL Server)
    var useSqlDatabase = builder.Configuration.GetValue<bool>("UseSqlDatabase");
    if (useSqlDatabase)
    {
        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("OrderDatabase")));
        builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
    }
    else
    {
        // Use in-memory repository for development/testing
        builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
    }

    // Register application services
    builder.Services.AddScoped<IEventPublisher, KafkaEventPublisher>();
    builder.Services.AddScoped<IOrderService, OrderManagementService>();

    // Add health checks
    var healthChecks = builder.Services.AddHealthChecks();

    if (useSqlDatabase)
    {
        healthChecks.AddSqlServer(
            builder.Configuration.GetConnectionString("OrderDatabase") ?? "",
            name: "sql-server",
            tags: new[] { "database", "sql" });
    }

    healthChecks.AddKafka(
        new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
        },
        name: "kafka",
        tags: new[] { "messaging", "kafka" });

    var app = builder.Build();

    // Run database migrations if using SQL
    if (useSqlDatabase)
    {
        await app.MigrateDatabaseAsync();
    }

    // Use Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);

            // Add correlation ID if present
            if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            {
                diagnosticContext.Set("CorrelationId", correlationId.ToString());
            }
        };
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseExceptionHandler("/error");

    app.Map("/error", (HttpContext http) =>
    {
        var ex = http.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = http.RequestServices.GetRequiredService<ILogger<Program>>();
        var env = http.RequestServices.GetRequiredService<IHostEnvironment>();

        logger.LogError(ex, "Unhandled exception");

        var statusCode = ex switch
        {
            System.ComponentModel.DataAnnotations.ValidationException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        var detail = env.IsProduction() ? null : ex?.Message; // redact in production

        // Minimal ProblemDetails response
        return Results.Problem(
            title: statusCode == 500 ? "An unexpected error occurred." : ex?.GetType().Name,
            detail: detail,
            statusCode: statusCode);
    });
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
