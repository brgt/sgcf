using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S16_AuditLogImpersonation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "impersonated_by",
                schema: "sgcf",
                table: "audit_log",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "impersonating",
                schema: "sgcf",
                table: "audit_log",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "impersonated_by",
                schema: "sgcf",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "impersonating",
                schema: "sgcf",
                table: "audit_log");
        }
    }
}
