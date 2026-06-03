using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Adiciona banco-alvo à cotação. Valida limite disponível antes de adicionar,
/// ramificando por regime do banco (SPEC_REGIME_LIMITE_EXPLICITO §4.3):
/// <list type="bullet">
///   <item><description>
///     <b>GlobalPuro</b>: exige <see cref="LimiteGlobalBanco"/> vigente e verifica o saldo
///     devedor agregado do banco. Sem pré-preenchimento de garantia (não há LimiteBanco).
///   </description></item>
///   <item><description>
///     <b>PerModalidade</b>: exige <see cref="LimiteBanco"/> para a modalidade. Quando há
///     <see cref="LimiteGlobalBanco"/> vigente, a disponibilidade efetiva é
///     <c>min(disponível_modalidade, disponível_global)</c>. Mantém o pré-preenchimento de garantia.
///   </description></item>
/// </list>
/// SPEC §3.2 regra 8, §4.3, §6.1, Task 4.1.
/// </summary>
public sealed record AdicionarBancoNaCotacaoCommand(
    Guid CotacaoId,
    Guid BancoId,
    /// <summary>
    /// Quando <c>true</c> (padrão), o handler deriva automaticamente os campos de garantia
    /// das <see cref="GarantiaExigidaItem"/> do limite e os inclui na resposta.
    /// Quando <c>false</c>, o caller deve informar os campos manualmente ao registrar a Proposta.
    /// </summary>
    bool PreencherGarantiaAutomaticamente = true,
    /// <summary>
    /// Valor manual de garantia informado pelo caller (para comparação com o pré-preenchimento
    /// e geração de alerta quando diverge). Opcional.
    /// </summary>
    string? GarantiaExigidaManual = null,
    /// <summary>Valor manual do valor da garantia em BRL para comparação com o calculado. Opcional.</summary>
    decimal? ValorGarantiaExigidaBrlManual = null,
    /// <summary>Flag manual de CDB cativo para comparação. Opcional.</summary>
    bool? GarantiaEhCdbCativoManual = null,
    /// <summary>
    /// Rendimento do CDB cativo em % a.a. Obrigatório quando o pré-preenchimento resultar em
    /// <c>GarantiaEhCdbCativo = true</c> (SPEC §3.3).
    /// </summary>
    decimal? RendimentoCdbAaPercentual = null) : IRequest<AdicionarBancoNaCotacaoResponse>;

public sealed class AdicionarBancoNaCotacaoCommandValidator : AbstractValidator<AdicionarBancoNaCotacaoCommand>
{
    public AdicionarBancoNaCotacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty().WithMessage("CotacaoId não pode ser vazio.");
        RuleFor(c => c.BancoId).NotEmpty().WithMessage("BancoId não pode ser vazio.");
    }
}

public sealed class AdicionarBancoNaCotacaoCommandHandler(
    ICotacaoRepository cotacaoRepo,
    ILimiteBancoRepository limiteRepo,
    ILimiteGlobalBancoRepository limiteGlobalRepo,
    IConsultaSaldoBanco saldo,
    ITenantContext tenantContext,
    IClock clock) : IRequestHandler<AdicionarBancoNaCotacaoCommand, AdicionarBancoNaCotacaoResponse>
{
    private static readonly DateTimeZone FusoHorarioBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<AdicionarBancoNaCotacaoResponse> Handle(
        AdicionarBancoNaCotacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        Cotacao cotacao = await cotacaoRepo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        Guid tenantId = tenantContext.TenantId;
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoHorarioBrasilia).Date;

        // SPEC_REGIME_LIMITE_EXPLICITO §4.1: o regime é lido da flag do banco
        // (BancoEmRegimePerModalityAsync lê Banco.RegimeLimite).
        bool perModalidade = await saldo.BancoEmRegimePerModalityAsync(cmd.BancoId, tenantId, cancellationToken);

        return perModalidade
            ? await AdicionarEmRegimePerModalidade(cmd, cotacao, tenantId, hoje, cancellationToken)
            : await AdicionarEmRegimeGlobalPuro(cmd, cotacao, tenantId, hoje, cancellationToken);
    }

    /// <summary>Regime GlobalPuro (§4.3): valida contra o limite global vigente; sem garantia pré-preenchida.</summary>
    private async Task<AdicionarBancoNaCotacaoResponse> AdicionarEmRegimeGlobalPuro(
        AdicionarBancoNaCotacaoCommand cmd, Cotacao cotacao, Guid tenantId,
        LocalDate hoje, CancellationToken ct)
    {
        LimiteGlobalBanco limiteGlobal = await limiteGlobalRepo.GetVigenteByBancoAsync(cmd.BancoId, hoje, ct)
            ?? throw new InvalidOperationException(
                $"Banco '{cmd.BancoId}' opera em regime de limite global, " +
                "mas não possui limite global vigente cadastrado. " +
                "Cadastre o limite global antes de operar. [REG-03]");

        Money saldoDevedor = await saldo.CalcularSaldoDevedorBancoAsync(cmd.BancoId, tenantId, ct);
        decimal disponivelGlobal = Math.Max(0m, limiteGlobal.ValorLimiteBrl.Valor - saldoDevedor.Valor);

        if (disponivelGlobal < cotacao.ValorAlvoBrl.Valor)
        {
            throw new InvalidOperationException(
                $"Banco '{cmd.BancoId}' não possui limite global disponível suficiente. " +
                $"Disponível: BRL {disponivelGlobal:F2}, " +
                $"necessário: BRL {cotacao.ValorAlvoBrl.Valor:F2}.");
        }

        cotacao.AdicionarBancoAlvo(cmd.BancoId);
        await cotacaoRepo.SaveChangesAsync(ct);

        return new AdicionarBancoNaCotacaoResponse(
            BancoId: cmd.BancoId,
            CotacaoId: cmd.CotacaoId,
            Proposta: null,
            Alertas: Array.Empty<string>());
    }

    /// <summary>Regime PerModalidade (§4.3): min(disponível modalidade, disponível global); mantém garantia.</summary>
    private async Task<AdicionarBancoNaCotacaoResponse> AdicionarEmRegimePerModalidade(
        AdicionarBancoNaCotacaoCommand cmd, Cotacao cotacao, Guid tenantId,
        LocalDate hoje, CancellationToken ct)
    {
        // SPEC §3.2 regra 8: banco precisa ter limite disponível >= ValorAlvoBRL
        LimiteBanco limite = await limiteRepo.GetByBancoModalidadeAsync(
            cmd.BancoId, cotacao.Modalidade, ct)
            ?? throw new InvalidOperationException(
                $"Banco '{cmd.BancoId}' não possui limite cadastrado para a modalidade '{cotacao.Modalidade}'. " +
                "Cadastre o limite operacional antes de adicionar o banco à cotação.");

        decimal disponivelModalidade = limite.ValorDisponivelBrl.Valor;
        LimiteGlobalBanco? limiteGlobal = await limiteGlobalRepo.GetVigenteByBancoAsync(cmd.BancoId, hoje, ct);

        if (limiteGlobal is not null)
        {
            Money utilizadoAgregado = await saldo.CalcularUtilizadoAgregadoModalidadesAsync(cmd.BancoId, tenantId, ct);
            decimal disponivelGlobal = Math.Max(0m, limiteGlobal.ValorLimiteBrl.Valor - utilizadoAgregado.Valor);
            decimal disponivelEfetivo = Math.Min(disponivelModalidade, disponivelGlobal);

            if (disponivelEfetivo < cotacao.ValorAlvoBrl.Valor)
            {
                string detalhe = disponivelGlobal < disponivelModalidade
                    ? $"Disponível (global): BRL {disponivelGlobal:F2}, " +
                      $"disponível (modalidade): BRL {disponivelModalidade:F2}, " +
                      $"necessário: BRL {cotacao.ValorAlvoBrl.Valor:F2}."
                    : $"Disponível (modalidade): BRL {disponivelModalidade:F2}, " +
                      $"necessário: BRL {cotacao.ValorAlvoBrl.Valor:F2}.";

                throw new InvalidOperationException(
                    $"Banco '{cmd.BancoId}' não possui limite disponível suficiente. " + detalhe);
            }
        }
        else if (disponivelModalidade < cotacao.ValorAlvoBrl.Valor)
        {
            throw new InvalidOperationException(
                $"Banco '{cmd.BancoId}' não possui limite disponível suficiente. " +
                $"Disponível: BRL {disponivelModalidade:F2}, " +
                $"necessário: BRL {cotacao.ValorAlvoBrl.Valor:F2}.");
        }

        cotacao.AdicionarBancoAlvo(cmd.BancoId);
        await cotacaoRepo.SaveChangesAsync(ct);

        // ── Pré-preenchimento Task 4.1 ────────────────────────────────────────
        GarantiaPreenchidaDto? garantiaPreenchida = null;
        List<string> alertas = [];

        if (cmd.PreencherGarantiaAutomaticamente && limite.GarantiasExigidas.Count > 0)
        {
            string garantiaExigidaCalculada =
                FormatadorGarantiaExigida.Formatar(limite.GarantiasExigidas);

            Money valorGarantiaCalculado =
                CalculadorValorGarantiaExigida.Calcular(limite.GarantiasExigidas, cotacao.ValorAlvoBrl);

            bool ehCdbCativo =
                limite.GarantiasExigidas.Any(g => g.Tipo == TipoGarantia.CdbCativo);

            // SPEC §3.3: se o pré-preenchimento resulta em GarantiaEhCdbCativo = true,
            // o caller DEVE fornecer RendimentoCdbAaPercentual.
            if (ehCdbCativo && cmd.RendimentoCdbAaPercentual is null)
            {
                throw new InvalidOperationException(
                    "O limite exige CDB cativo como garantia. " +
                    "Forneça 'rendimentoCdbAaPercentual' para prosseguir com pré-preenchimento automático (SPEC §3.3), " +
                    "ou utilize 'preencherGarantiaAutomaticamente = false' para informar os dados manualmente.");
            }

            garantiaPreenchida = new GarantiaPreenchidaDto(
                GarantiaExigida: garantiaExigidaCalculada,
                ValorGarantiaExigidaBrl: valorGarantiaCalculado.Valor,
                GarantiaEhCdbCativo: ehCdbCativo);

            // ── Task 4.2: alertas de coerência ────────────────────────────────
            alertas.AddRange(GerarAlertasCoerencia(cmd, garantiaExigidaCalculada, valorGarantiaCalculado.Valor, ehCdbCativo));
        }

        return new AdicionarBancoNaCotacaoResponse(
            BancoId: cmd.BancoId,
            CotacaoId: cmd.CotacaoId,
            Proposta: garantiaPreenchida,
            Alertas: alertas.AsReadOnly());
    }

    /// <summary>
    /// Gera alertas informativos quando os valores manuais fornecidos pelo caller divergem
    /// do que seria calculado pelo pré-preenchimento automático. Não bloqueia a operação.
    /// Task 4.2.
    /// </summary>
    private static IEnumerable<string> GerarAlertasCoerencia(
        AdicionarBancoNaCotacaoCommand cmd,
        string garantiaExigidaCalculada,
        decimal valorGarantiaCalculado,
        bool ehCdbCativoCalculado)
    {
        if (cmd.GarantiaExigidaManual is not null &&
            !string.Equals(cmd.GarantiaExigidaManual, garantiaExigidaCalculada, StringComparison.Ordinal))
        {
            yield return
                $"O campo 'garantiaExigida' informado manualmente ('{cmd.GarantiaExigidaManual}') " +
                $"diverge do valor calculado automaticamente ('{garantiaExigidaCalculada}').";
        }

        if (cmd.ValorGarantiaExigidaBrlManual.HasValue &&
            cmd.ValorGarantiaExigidaBrlManual.Value != valorGarantiaCalculado)
        {
            yield return
                $"O campo 'valorGarantiaExigidaBrl' informado manualmente ({cmd.ValorGarantiaExigidaBrlManual.Value:F2}) " +
                $"diverge do valor calculado automaticamente ({valorGarantiaCalculado:F2}).";
        }

        if (cmd.GarantiaEhCdbCativoManual.HasValue &&
            cmd.GarantiaEhCdbCativoManual.Value != ehCdbCativoCalculado)
        {
            yield return
                $"O campo 'garantiaEhCdbCativo' informado manualmente ({cmd.GarantiaEhCdbCativoManual.Value}) " +
                $"diverge do valor calculado automaticamente ({ehCdbCativoCalculado}).";
        }
    }
}
