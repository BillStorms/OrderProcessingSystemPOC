
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using AspNetCoreRateLimit;
using Asp.Versioning;
using OrderService.Service.Interfaces;
using OrderService.Service.Services;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.Kafka;
using OrderService.Api.Extensions;
using OrderService.Api.Middleware;
using Serilog;
using Serilog.Formatting.Json;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddJsonFile("appsettings.RateLimit.json", optional: true)
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

    // Add configuration for rate limiting
    builder.Configuration.AddJsonFile("appsettings.RateLimit.json", optional: true, reloadOnChange: true);

    // Add Serilog
    builder.Host.UseSerilog();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DefaultCorsPolicy", policy =>
        {
            policy.WithOrigins(
                    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                    ?? new[] { "http://localhost:3000", "http://localhost:5173" })
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // Configure Rate Limiting
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // Configure API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-Api-Version"),
            new QueryStringApiVersionReader("api-version"));
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
        { 
            Title = "Order Service API", 
            Version = "v1",
            Description = "Order processing service with event-driven architecture"
        });
    });

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
        // Singleton ensures orders persist across requests for POC demo
        builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
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

    // Security: Add security headers middleware
    app.UseSecurityHeaders();

    // Security: Enable IP rate limiting
    app.UseIpRateLimiting();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Security: Enable CORS
    app.UseCors("DefaultCorsPolicy");

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
