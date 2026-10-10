using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Saas.Infrastructure.Persistence;

namespace Saas.Api.Hosting;

/// <summary>Base de données au démarrage, en Development seulement (DEPLOYMENT.md § Migrations de base de données).</summary>
internal static class DatabaseStartupExtensions
{
    /// <summary>Crée la base au besoin et applique les migrations, si <c>Database:MigrateOnStartup</c> est vrai.</summary>
    public static async Task MigrateDatabaseInDevelopmentAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Créées avant MigrateAsync, qui journaliserait sinon en erreur l'absence de la table d'historique.
            var creator = db.GetService<IRelationalDatabaseCreator>();
            if (!await creator.ExistsAsync())
            {
                await creator.CreateAsync();
            }

            await db.GetService<IHistoryRepository>().CreateIfNotExistsAsync();
            await db.Database.MigrateAsync();
        });
    }
}
