using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.Kafka;
using OrderService.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Run database migrations if using SQL
if (useSqlDatabase)
{
    await app.MigrateDatabaseAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
