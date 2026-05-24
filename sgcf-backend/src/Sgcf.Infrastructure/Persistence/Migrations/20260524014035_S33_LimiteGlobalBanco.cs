using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S33_LimiteGlobalBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "limite_global_banco",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_limite_brl = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    data_vigencia_inicio = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vigencia_fim = table.Column<LocalDate>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_global_banco", x => x.id);
                    table.ForeignKey(
                        name: "FK_limite_global_banco_banco_config_banco_id",
                        column: x => x.banco_id,
                        principalSchema: "sgcf",
                        principalTable: "banco_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "limite_global_banco_historico",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    limite_global_banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_anterior_brl = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    valor_novo_brl = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_global_banco_historico", x => x.id);
                    table.ForeignKey(
                        name: "FK_limite_global_banco_historico_limite_global_banco_limite_gl~",
                        column: x => x.limite_global_banco_id,
                        principalSchema: "sgcf",
                        principalTable: "limite_global_banco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_limite_global_banco_banco_id",
                schema: "sgcf",
                table: "limite_global_banco",
                column: "banco_id");

            migrationBuilder.CreateIndex(
                name: "ix_limite_global_banco_banco_vigente_uq",
                schema: "sgcf",
                table: "limite_global_banco",
                columns: new[] { "tenant_id", "banco_id" },
                unique: true,
                filter: "data_vigencia_fim IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_limite_global_banco_tenant_id",
                schema: "sgcf",
                table: "limite_global_banco",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_limite_global_banco_historico_limite_id",
                schema: "sgcf",
                table: "limite_global_banco_historico",
                column: "limite_global_banco_id");

            migrationBuilder.CreateIndex(
                name: "ix_limite_global_banco_historico_limite_registrado_em",
                schema: "sgcf",
                table: "limite_global_banco_historico",
                columns: new[] { "limite_global_banco_id", "registrado_em" });

            // RLS: limite_global_banco tem tenant_id — isola por tenant.
            // limite_global_banco_historico não tem tenant_id (sem ITenantScoped);
            // o isolamento ocorre indiretamente via JOIN com a tabela pai.
            migrationBuilder.Sql("ALTER TABLE sgcf.limite_global_banco ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY tenant_isolation ON sgcf.limite_global_banco USING (tenant_id = current_setting('app.tenant_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON sgcf.limite_global_banco;");
            migrationBuilder.Sql("ALTER TABLE sgcf.limite_global_banco DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "limite_global_banco_historico",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "limite_global_banco",
                schema: "sgcf");
        }
    }
}
