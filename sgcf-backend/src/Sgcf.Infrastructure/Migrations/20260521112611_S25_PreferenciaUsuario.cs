using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S25_PreferenciaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "preferencia_usuario",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    chave = table.Column<string>(type: "text", maxLength: 100, nullable: false),
                    valor = table.Column<string>(type: "text", maxLength: 4000, nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preferencia_usuario", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_preferencia_usuario_tenant_user_chave",
                schema: "sgcf",
                table: "preferencia_usuario",
                columns: new[] { "tenant_id", "user_id", "chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "preferencia_usuario",
                schema: "sgcf");
        }
    }
}
