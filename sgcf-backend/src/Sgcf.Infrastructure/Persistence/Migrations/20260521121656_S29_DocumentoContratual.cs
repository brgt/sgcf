using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S29_DocumentoContratual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documento_contratual",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    url_armazenamento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_emissao = table.Column<LocalDate>(type: "date", nullable: true),
                    data_vencimento = table.Column<LocalDate>(type: "date", nullable: true),
                    observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_contratual", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documento_contratual_contrato_id",
                schema: "sgcf",
                table: "documento_contratual",
                columns: new[] { "tenant_id", "contrato_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documento_contratual",
                schema: "sgcf");
        }
    }
}
