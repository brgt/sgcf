using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Fase4SimulacaoAntecipacaoSource : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "source",
            schema: "sgcf",
            table: "simulacao_antecipacao",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "API");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "source",
            schema: "sgcf",
            table: "simulacao_antecipacao");
    }
}
