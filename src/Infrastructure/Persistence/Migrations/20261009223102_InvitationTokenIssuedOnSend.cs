using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvitationTokenIssuedOnSend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "Invitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Une valeur distincte par invitation sans lien, pour l'index unique. Un MD5 (32 caractères)
            // ne peut égaler aucun hash SHA-256 (64) : ces invitations restent sans lien valable.
            migrationBuilder.Sql("""UPDATE "Invitations" SET "TokenHash" = md5("Id"::text) WHERE "TokenHash" IS NULL;""");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "Invitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
