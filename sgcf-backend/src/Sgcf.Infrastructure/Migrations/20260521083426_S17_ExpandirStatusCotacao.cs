using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <summary>
    /// Expande <c>StatusCotacao</c> com os valores <c>EmAnaliseBanco = 7</c> e <c>PropostaRecebida = 8</c>.
    ///
    /// Esta migration não altera o esquema do banco de dados porque:
    /// - A coluna <c>status</c> na tabela <c>cotacao</c> é do tipo <c>smallint</c> (PostgreSQL),
    ///   sem CHECK constraint explícito sobre os valores permitidos.
    /// - Os novos valores 7 e 8 estão dentro do range de <c>smallint</c> (-32768 a 32767).
    /// - Dados existentes (valores 1–6) são preservados sem alteração.
    ///
    /// A migration existe como marcador de versão no histórico do EF Core e para manter
    /// o model snapshot em sincronia com o estado atual do domínio.
    /// </summary>
    public partial class S17_ExpandirStatusCotacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem alterações de esquema necessárias. Ver comentário da classe.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem alterações de esquema para reverter. Ver comentário da classe.
        }
    }
}
