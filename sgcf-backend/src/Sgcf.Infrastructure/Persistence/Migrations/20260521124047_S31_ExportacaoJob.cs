using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S31_ExportacaoJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exportacao_job",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    parametros_json = table.Column<string>(type: "text", nullable: true),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    mensagem_erro = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    iniciado_em = table.Column<Instant>(type: "timestamptz", nullable: true),
                    concluido_em = table.Column<Instant>(type: "timestamptz", nullable: true),
                    solicitado_por = table.Column<string>(type: "text", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exportacao_job", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exportacao_job_solicitado_por",
                schema: "sgcf",
                table: "exportacao_job",
                columns: new[] { "tenant_id", "solicitado_por" });

            migrationBuilder.CreateIndex(
                name: "ix_exportacao_job_tenant_status",
                schema: "sgcf",
                table: "exportacao_job",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exportacao_job",
                schema: "sgcf");
        }
    }
}
