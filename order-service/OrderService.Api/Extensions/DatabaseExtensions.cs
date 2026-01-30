using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Data;

namespace OrderService.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        
        try
        {
            var context = services.GetRequiredService<OrderDbContext>();
            await context.Database.MigrateAsync();
            
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating the database");
            throw;
        }
    }
}
