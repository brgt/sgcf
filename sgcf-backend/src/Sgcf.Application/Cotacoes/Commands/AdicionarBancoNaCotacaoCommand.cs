using FluentValidation;
using MediatR;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Adiciona banco-alvo à cotação. Valida limite disponível antes de adicionar.
/// Quando <see cref="PreencherGarantiaAutomaticamente"/> é <c>true</c> e o limite possui
/// garantias exigidas, retorna template de garantia pré-preenchido para facilitar o registro
/// posterior da Proposta.
/// SPEC §3.2 regra 8, §6.1, Task 4.1.
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
    ILimiteBancoRepository limiteRepo) : IRequestHandler<AdicionarBancoNaCotacaoCommand, AdicionarBancoNaCotacaoResponse>
{
    public async Task<AdicionarBancoNaCotacaoResponse> Handle(
        AdicionarBancoNaCotacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        Cotacao cotacao = await cotacaoRepo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        // SPEC §3.2 regra 8: banco precisa ter limite disponível >= ValorAlvoBRL
        LimiteBanco limite = await limiteRepo.GetByBancoModalidadeAsync(
            cmd.BancoId,
            cotacao.Modalidade,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Banco '{cmd.BancoId}' não possui limite cadastrado para a modalidade '{cotacao.Modalidade}'. " +
                "Cadastre o limite operacional antes de adicionar o banco à cotação.");

        if (limite.ValorDisponivelBrl.Valor < cotacao.ValorAlvoBrl.Valor)
        {
            throw new InvalidOperationException(
                $"Banco '{cmd.BancoId}' não possui limite disponível suficiente. " +
                $"Disponível: BRL {limite.ValorDisponivelBrl.Valor:F2}, " +
                $"necessário: BRL {cotacao.ValorAlvoBrl.Valor:F2}.");
        }

        cotacao.AdicionarBancoAlvo(cmd.BancoId);
        await cotacaoRepo.SaveChangesAsync(cancellationToken);

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
            // A validação é feita aqui (não duplicada no domínio — Proposta valida apenas
            // em seu construtor, que é invocado por RegistrarPropostaCommand).
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
