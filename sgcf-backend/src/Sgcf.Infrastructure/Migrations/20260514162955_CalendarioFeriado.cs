using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CalendarioFeriado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feriado",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<LocalDate>(type: "date", nullable: false),
                    tipo = table.Column<byte>(type: "smallint", nullable: false),
                    escopo = table.Column<byte>(type: "smallint", nullable: false),
                    descricao = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    fonte = table.Column<byte>(type: "smallint", nullable: false),
                    ano_referencia = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feriado", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feriado_ano_referencia_escopo",
                schema: "sgcf",
                table: "feriado",
                columns: new[] { "ano_referencia", "escopo" });

            migrationBuilder.CreateIndex(
                name: "IX_feriado_data_tipo_escopo",
                schema: "sgcf",
                table: "feriado",
                columns: new[] { "data", "tipo", "escopo" },
                unique: true);

            // ── Seed de feriados nacionais ANBIMA 2026 e 2027 ───────────────
            // Páscoa 2026: 05/04 → Sex Paixão 03/04; Carnaval 16-17/02; Corpus 04/06
            // Páscoa 2027: 28/03 → Sex Paixão 26/03; Carnaval 08-09/02; Corpus 27/05
            // Convenção de tipos:
            //   tipo: 1=Nacional, 2=Bancario, 3=BolsaB3, 4=Regional
            //   escopo: 1=Brasil
            //   fonte: 1=Anbima
            migrationBuilder.Sql(@"
                INSERT INTO sgcf.feriado (id, data, tipo, escopo, descricao, fonte, ano_referencia, created_at, updated_at) VALUES
                  (gen_random_uuid(), '2026-01-01', 1, 1, 'Confraternização Universal', 1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-02-16', 2, 1, 'Carnaval (segunda-feira)',   1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-02-17', 2, 1, 'Carnaval (terça-feira)',     1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-04-03', 1, 1, 'Sexta-feira da Paixão',      1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-04-21', 1, 1, 'Tiradentes',                 1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-05-01', 1, 1, 'Dia do Trabalho',            1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-06-04', 2, 1, 'Corpus Christi',             1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-09-07', 1, 1, 'Independência do Brasil',    1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-10-12', 1, 1, 'Nossa Senhora Aparecida',    1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-11-02', 1, 1, 'Finados',                    1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-11-15', 1, 1, 'Proclamação da República',   1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-11-20', 1, 1, 'Consciência Negra',          1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2026-12-25', 1, 1, 'Natal',                      1, 2026, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-01-01', 1, 1, 'Confraternização Universal', 1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-02-08', 2, 1, 'Carnaval (segunda-feira)',   1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-02-09', 2, 1, 'Carnaval (terça-feira)',     1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-03-26', 1, 1, 'Sexta-feira da Paixão',      1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-04-21', 1, 1, 'Tiradentes',                 1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-05-01', 1, 1, 'Dia do Trabalho',            1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-05-27', 2, 1, 'Corpus Christi',             1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-09-07', 1, 1, 'Independência do Brasil',    1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-10-12', 1, 1, 'Nossa Senhora Aparecida',    1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-11-02', 1, 1, 'Finados',                    1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-11-15', 1, 1, 'Proclamação da República',   1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-11-20', 1, 1, 'Consciência Negra',          1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'),
                  (gen_random_uuid(), '2027-12-25', 1, 1, 'Natal',                      1, 2027, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feriado",
                schema: "sgcf");
        }
    }
}
