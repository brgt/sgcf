using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S18_AlertasUnificados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alertas",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria = table.Column<byte>(type: "smallint", nullable: false),
                    severidade = table.Column<byte>(type: "smallint", nullable: false),
                    titulo = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    origem_tipo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acao_rotulo = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    acao_rota = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    expira_em = table.Column<Instant>(type: "timestamptz", nullable: true),
                    chave_idempotencia = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerta_perfil_visivel",
                schema: "sgcf",
                columns: table => new
                {
                    alerta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta_perfil_visivel", x => new { x.alerta_id, x.perfil });
                    table.ForeignKey(
                        name: "FK_alerta_perfil_visivel_alertas_alerta_id",
                        column: x => x.alerta_id,
                        principalSchema: "sgcf",
                        principalTable: "alertas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_tenant_status_severidade",
                schema: "sgcf",
                table: "alertas",
                columns: new[] { "tenant_id", "status", "severidade" });

            migrationBuilder.CreateIndex(
                name: "ux_alertas_chave_idempotencia",
                schema: "sgcf",
                table: "alertas",
                column: "chave_idempotencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta_perfil_visivel",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "alertas",
                schema: "sgcf");
        }
    }
}
