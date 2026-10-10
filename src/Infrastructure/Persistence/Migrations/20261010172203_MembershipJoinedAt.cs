using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MembershipJoinedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "JoinedAt",
                table: "OrganizationMemberships",
                type: "timestamp with time zone",
                nullable: true);

            // Adhésions existantes : leur Id a toujours été un UUID v7 attribué à la création
            // (Guid.CreateVersion7(), dans l'initialiseur de BaseEntity à l'origine, puis dans
            // OrganizationMembership.Create). Ses 48 premiers bits sont l'instant de création en
            // millisecondes Unix : c'est la date d'arrivée, à la milliseconde. Un Id d'une autre
            // version (ligne insérée à la main, par exemple) reçoit la date de création de
            // l'organisation plutôt qu'une date tirée de bits aléatoires.
            migrationBuilder.Sql("""
                UPDATE "OrganizationMemberships" AS m
                SET "JoinedAt" = CASE
                    WHEN substr(m."Id"::text, 15, 1) = '7'
                        THEN to_timestamp(('x' || substr(replace(m."Id"::text, '-', ''), 1, 12))::bit(48)::bigint / 1000.0)
                    ELSE o."CreatedAt"
                END
                FROM "Organizations" AS o
                WHERE o."Id" = m."OrganizationId";
                """);

            // Le défaut now() couvre la version précédente de l'API, encore en service pendant que
            // cette migration s'applique : elle insère des adhésions sans JoinedAt.
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "JoinedAt",
                table: "OrganizationMemberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "OrganizationMemberships");
        }
    }
}
