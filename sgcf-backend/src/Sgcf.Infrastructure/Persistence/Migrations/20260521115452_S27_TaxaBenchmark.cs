using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S27_TaxaBenchmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "taxa_benchmark",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_benchmark = table.Column<string>(type: "text", maxLength: 50, nullable: false),
                    data_referencia = table.Column<LocalDate>(type: "date", nullable: false),
                    taxa_aa_decimal = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    fonte = table.Column<string>(type: "text", maxLength: 50, nullable: false),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxa_benchmark", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_taxa_benchmark_tenant_tipo_data",
                schema: "sgcf",
                table: "taxa_benchmark",
                columns: new[] { "tenant_id", "tipo_benchmark", "data_referencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "taxa_benchmark",
                schema: "sgcf");
        }
    }
}
