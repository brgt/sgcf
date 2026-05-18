using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Cotacoes.Conversores;

/// <summary>
/// Conversor da modalidade Lei 4131/62 (empréstimo direto do exterior).
/// <para>
/// Cria <see cref="Lei4131Detail"/> a partir dos campos persistíveis do
/// <see cref="Lei4131Inputs"/> presente no command. Os campos informativos
/// (<c>PaisCredor</c> e <c>AliquotaIrrfPercentual</c>) são descartados após
/// o cálculo — não persistidos no MVP (decisão MD-5/AD-3 do plano Lei 4131).
/// </para>
/// Onda 4 — SPEC §6.1 (docs/specs/cotacoes/modalidades/lei4131.md).
/// </summary>
public sealed class ConversorLei4131 : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Lei4131;

    /// <inheritdoc/>
    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        if (ctx.Command.Lei4131 is null)
        {
            throw new InvalidOperationException(
                "Lei4131Detail é obrigatório para conversão de cotação Lei 4131. " +
                "Informe os campos lei4131 no payload do command. SPEC §5.3.");
        }

        Lei4131Inputs input = ctx.Command.Lei4131;

        Lei4131Detail detail = Lei4131Detail.Criar(
            contratoId: ctx.ContratoCriado.Id,
            sblcNumero: input.SblcNumero,
            sblcBancoEmissor: input.SblcBancoEmissor,
            sblcValorUsd: input.SblcValorUsd,
            temMarketFlex: input.TemMarketFlex,
            breakFundingFeePercentual: input.BreakFundingFeePercentual,
            clock: ctx.Clock);

        // PaisCredor e AliquotaIrrfPercentual NÃO são persistidos (MD-5/AD-3).
        // São descartados aqui; futuramente podem compor o snapshot de EconomiaNegociacao.

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
