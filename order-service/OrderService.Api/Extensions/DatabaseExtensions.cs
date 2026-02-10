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

            // If there are migrations in the assembly apply them; otherwise create schema directly for dev.
            var migrations = context.Database.GetMigrations();
            if (migrations != null && migrations.Any())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                // If migrations aren't present, ensure the schema exists. However if a
                // __EFMigrationsHistory table exists but no tables were created previously,
                // EnsureCreated will be skipped. Check for the existence of the Orders table
                // and call EnsureCreated only when the table is missing.
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT OBJECT_ID('dbo.Orders', 'U')";
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj == null || obj == DBNull.Value)
                    {
                        await context.Database.EnsureCreatedAsync();
                    }
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }

            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Database migration/creation completed successfully");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating/creating the database");
            throw;
        }
    }
}
