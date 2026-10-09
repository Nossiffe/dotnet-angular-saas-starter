using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op DDL volontaire : `xmin` est une colonne système Postgres, présente sur chaque
            // table. On ne fait que la mapper comme token de concurrence optimiste (cf.
            // ProductConfiguration / OrganizationConfiguration). Aucune colonne à créer — le
            // `migrationBuilder.AddColumn("xmin", ...)` généré par EF échouerait à l'exécution
            // (impossible d'ajouter une colonne système). Cette migration existe uniquement pour
            // garder le snapshot du modèle en phase (sinon PendingModelChangesWarning au MigrateAsync).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op : voir Up — rien n'a été créé, donc rien à supprimer.
        }
    }
}
