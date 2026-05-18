using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Conversores;

/// <summary>
/// Conversor para a modalidade FGI (Fundo Garantidor para Investimentos — programa BNDES).
/// Cria <see cref="FgiDetail"/> a partir dos inputs do <see cref="ConverterEmContratoCommand"/>
/// e retorna <c>(FgiDetail, null)</c> — FGI-modalidade não possui detail secundário.
/// <para>
/// Responsabilidades deste conversor (SPEC §6.1):
/// <list type="bullet">
///   <item>Validar que <c>TaxaFgiAaPercentual</c> está presente e é maior que zero (EC-2, EC-13).</item>
///   <item>Validar que <c>PercentualCoberto</c>, quando informado, está no intervalo (0, 100] (EC-3, EC-4).</item>
///   <item>Criar <see cref="FgiDetail"/> via factory, passando valores em percentual humano.</item>
/// </list>
/// </para>
/// <para>
/// <see cref="FgiInputs.PercentualCoberto"/> é informativo — não entra no CET (SPEC §7.2, §2.3).
/// </para>
/// Onda 3a — SPEC docs/specs/cotacoes/modalidades/fgi.md §6.
/// </summary>
public sealed class ConversorFgi : IConversorModalidade
{
    /// <inheritdoc/>
    public ModalidadeContrato Modalidade => ModalidadeContrato.Fgi;

    /// <inheritdoc/>
    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx,
        CancellationToken cancellationToken)
    {
        ConverterEmContratoCommand cmd = ctx.Command;
        FgiInputs? fgi = cmd.Fgi;

        // Guard: TaxaFgiAaPercentual é obrigatória e deve ser positiva (EC-2, EC-13).
        // Sem essa taxa, a modalidade não teria tarifa FGI — registrar como outra modalidade.
        if (fgi is null || fgi.TaxaFgiAaPercentual <= 0m)
        {
            throw new InvalidOperationException(
                "TaxaFgiAaPercentual é obrigatória e deve ser > 0 para modalidade FGI " +
                "(SPEC §5.3, EC-2, EC-13). Informe taxaFgiAaPercentual no command de conversão.");
        }

        // Guard: PercentualCoberto, quando informado, deve estar em (0, 100] (EC-3, EC-4).
        // Cobertura de 0% é degenerada (usar null se indefinida); > 100% é fisicamente impossível.
        if (fgi.PercentualCoberto is decimal pc)
        {
            if (pc > 100m)
            {
                throw new InvalidOperationException(
                    $"PercentualCoberto deve ser ≤ 100% (recebido: {pc}). " +
                    "Cobertura acima de 100% é fisicamente impossível (EC-3).");
            }

            if (pc <= 0m)
            {
                throw new InvalidOperationException(
                    $"PercentualCoberto deve ser > 0 quando informado (recebido: {pc}). " +
                    "Cobertura zero não faz sentido — use null se a cobertura for indefinida (EC-4).");
            }
        }

        FgiDetail detail = FgiDetail.Criar(
            contratoId: ctx.ContratoCriado.Id,
            numeroOperacaoFgi: cmd.NumeroOperacaoFgi,
            taxaFgiAaPct: fgi.TaxaFgiAaPercentual,
            percentualCobertoPct: fgi.PercentualCoberto,
            clock: ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
