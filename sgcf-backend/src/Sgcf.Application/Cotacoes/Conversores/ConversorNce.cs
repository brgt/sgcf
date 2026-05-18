using NodaTime;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Cotacoes.Conversores;

/// <summary>
/// Conversor para a modalidade NCE (Nota de Crédito à Exportação).
/// Cria <see cref="NceDetail"/> a partir dos inputs do <see cref="ConverterEmContratoCommand"/>
/// e retorna <c>(NceDetail, null)</c> — NCE não possui detail secundário.
/// <para>
/// Todos os campos do NceDetail são opcionais: se <see cref="ConverterEmContratoCommand.Nce"/>
/// for null ou tiver campos null, o detail é persistido com esses campos null,
/// alinhado ao comportamento de <c>CreateContratoCommand</c> linha 313.
/// </para>
/// SPEC §6 — Onda 2 (docs/specs/cotacoes/modalidades/nce.md).
/// </summary>
public sealed class ConversorNce : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Nce;

    /// <inheritdoc/>
    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        NceInputs? inputs = ctx.Command.Nce;

        // Converter DateOnly? para LocalDate? — NodaTime é o tipo canônico no domínio.
        LocalDate? dataEmissao = inputs?.DataEmissao.HasValue == true
            ? new LocalDate(
                inputs.DataEmissao.Value.Year,
                inputs.DataEmissao.Value.Month,
                inputs.DataEmissao.Value.Day)
            : (LocalDate?)null;

        NceDetail detail = NceDetail.Criar(
            contratoId: ctx.ContratoCriado.Id,
            nceNumero: inputs?.NceNumero,
            dataEmissao: dataEmissao,
            bancoMandatario: inputs?.BancoMandatario,
            clock: ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
