using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S21_ContaBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conta_bancaria",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    agencia = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    numero_conta = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    moeda = table.Column<string>(type: "char(3)", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conta_bancaria", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conta_bancaria_banco_id",
                schema: "sgcf",
                table: "conta_bancaria",
                column: "banco_id");

            migrationBuilder.CreateIndex(
                name: "ux_conta_bancaria_tenant_agencia_numero",
                schema: "sgcf",
                table: "conta_bancaria",
                columns: new[] { "tenant_id", "agencia", "numero_conta" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conta_bancaria",
                schema: "sgcf");
        }
    }
}
