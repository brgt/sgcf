using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase2CronogramaPagamento : Migration
    {
        private static readonly string[] IndexColumnsContratoPrevistasTipo = new[] { "contrato_id", "data_prevista", "tipo" };
        private static readonly string[] IndexColumnsContratoNumeroEvento = new[] { "contrato_id", "numero_evento" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cronograma_pagamento",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_evento = table.Column<short>(type: "smallint", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    data_prevista = table.Column<LocalDate>(type: "date", nullable: false),
                    valor_moeda_original = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_brl_estimado = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    saldo_devedor_apos = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    moeda = table.Column<string>(type: "char(3)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    data_pagamento_efetivo = table.Column<LocalDate>(type: "date", nullable: true),
                    valor_pagamento_efetivo = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    valor_pagamento_efetivo_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    taxa_cambio_pagamento = table.Column<decimal>(type: "numeric(12,6)", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    comprovante_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cronograma_pagamento", x => x.id);
                    table.CheckConstraint("ck_cronograma_valor_nao_negativo", "valor_moeda_original >= 0");
                    table.ForeignKey(
                        name: "FK_cronograma_pagamento_contrato_contrato_id",
                        column: x => x.contrato_id,
                        principalSchema: "sgcf",
                        principalTable: "contrato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cronograma_pagamento_contrato_id_data_prevista_tipo",
                schema: "sgcf",
                table: "cronograma_pagamento",
                columns: IndexColumnsContratoPrevistasTipo);

            migrationBuilder.CreateIndex(
                name: "IX_cronograma_pagamento_contrato_id_numero_evento",
                schema: "sgcf",
                table: "cronograma_pagamento",
                columns: IndexColumnsContratoNumeroEvento);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cronograma_pagamento",
                schema: "sgcf");
        }
    }
}
