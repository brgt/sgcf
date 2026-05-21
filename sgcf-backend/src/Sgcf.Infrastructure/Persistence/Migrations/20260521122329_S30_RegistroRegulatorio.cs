using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S30_RegistroRegulatorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registro_regulatorio",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    numero_registro = table.Column<string>(type: "text", maxLength: 200, nullable: true),
                    data_registro = table.Column<LocalDate>(type: "date", nullable: true),
                    data_vencimento = table.Column<LocalDate>(type: "date", nullable: true),
                    observacao = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registro_regulatorio", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_registro_regulatorio_contrato_id",
                schema: "sgcf",
                table: "registro_regulatorio",
                columns: new[] { "tenant_id", "contrato_id" });

            migrationBuilder.CreateIndex(
                name: "ix_registro_regulatorio_tenant_status",
                schema: "sgcf",
                table: "registro_regulatorio",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registro_regulatorio",
                schema: "sgcf");
        }
    }
}
