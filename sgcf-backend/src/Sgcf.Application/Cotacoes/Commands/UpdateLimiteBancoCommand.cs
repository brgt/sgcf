using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza limite operacional com semântica PATCH. SPEC §6.1.
/// NovoValorLimiteBrl null = preservar valor atual.
/// GarantiasExigidas null = preservar garantias atuais; lista vazia = remover todas; populada = substituir todas.
/// Campos de antecipação: quando o campo está ausente (null) no request, é preservado o valor atual.
/// Para limpar um campo, envie explicitamente null.
/// </summary>
public sealed record UpdateLimiteBancoCommand(
    Guid LimiteId,
    decimal? NovoValorLimiteBrl = null,
    IReadOnlyList<CriarGarantiaExigidaItemRequest>? GarantiasExigidas = null,
    bool ConfigurarAntecipacao = false,
    string? PadraoAntecipacao = null,
    decimal? BreakFundingFeePct = null,
    decimal? TlaPctSobreSaldo = null,
    decimal? TlaPctPorMesRemanescente = null,
    decimal? ValorMinimoParcialPct = null,
    string? ObservacoesAntecipacao = null) : IRequest<LimiteBancoDto>;

public sealed class UpdateLimiteBancoCommandValidator : AbstractValidator<UpdateLimiteBancoCommand>
{
    public UpdateLimiteBancoCommandValidator()
    {
        RuleFor(c => c.LimiteId).NotEmpty();

        RuleFor(c => c.NovoValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("NovoValorLimiteBrl deve ser maior que zero.")
            .When(c => c.NovoValorLimiteBrl.HasValue);

        // Validate enum names only — XOR delegated to domain.
        RuleForEach(c => c.GarantiasExigidas)
            .ChildRules(g =>
                g.RuleFor(r => r.Tipo)
                 .NotEmpty()
                 .Must(v => Enum.TryParse<TipoGarantia>(v, ignoreCase: true, out _))
                 .WithMessage(r => $"Tipo de garantia inválido: '{r.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}."))
            .When(c => c.GarantiasExigidas is not null);

        RuleFor(c => c.PadraoAntecipacao)
            .Must(v => Enum.TryParse<Domain.Common.PadraoAntecipacao>(v!, true, out _))
            .WithMessage($"PadraoAntecipacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Common.PadraoAntecipacao>())}.")
            .When(c => c.ConfigurarAntecipacao && c.PadraoAntecipacao is not null);

        RuleFor(c => c.BreakFundingFeePct).GreaterThanOrEqualTo(0).When(c => c.BreakFundingFeePct.HasValue);
        RuleFor(c => c.TlaPctSobreSaldo).GreaterThanOrEqualTo(0).When(c => c.TlaPctSobreSaldo.HasValue);
        RuleFor(c => c.TlaPctPorMesRemanescente).GreaterThanOrEqualTo(0).When(c => c.TlaPctPorMesRemanescente.HasValue);
        RuleFor(c => c.ValorMinimoParcialPct).GreaterThanOrEqualTo(0).When(c => c.ValorMinimoParcialPct.HasValue);
    }
}

public sealed class UpdateLimiteBancoCommandHandler(
    ILimiteBancoRepository repo,
    ILimiteGlobalBancoRepository limiteGlobalRepo,
    IClock clock)
    : IRequestHandler<UpdateLimiteBancoCommand, LimiteBancoDto>
{
    public async Task<LimiteBancoDto> Handle(UpdateLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        // GetByIdAsync already eager-loads GarantiasExigidas + Historico (see repository).
        // AsNoTracking is used there, so we need tracking for the update path.
        LimiteBanco limite = await repo.GetByIdTrackingAsync(cmd.LimiteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{cmd.LimiteId}' não encontrado.");

        if (cmd.NovoValorLimiteBrl.HasValue)
        {
            // LG-09: o novo valor do limite por modalidade não pode superar o limite global vigente do banco.
            LimiteGlobalBanco? limiteGlobal = await limiteGlobalRepo.GetVigenteByBancoAsync(limite.BancoId, cancellationToken);
            if (limiteGlobal is not null)
            {
                Money novoValorVerificacao = new(cmd.NovoValorLimiteBrl.Value, Moeda.Brl);
                if (novoValorVerificacao.MaiorQue(limiteGlobal.ValorLimiteBrl))
                {
                    throw new InvalidOperationException(
                        $"O valor do limite por modalidade ({novoValorVerificacao}) não pode superar o limite global vigente do banco ({limiteGlobal.ValorLimiteBrl}). [LG-09]");
                }
            }

            Money novoValor = new(cmd.NovoValorLimiteBrl.Value, Moeda.Brl);
            limite.Atualizar(clock, novoLimiteBrl: novoValor);
        }

        if (cmd.GarantiasExigidas is not null)
        {
            IEnumerable<GarantiaExigidaItemSpec> specs = cmd.GarantiasExigidas.Select(r => r.ParaSpec());
            limite.SubstituirGarantiasExigidas(specs, clock);
        }

        if (cmd.ConfigurarAntecipacao)
        {
            Domain.Common.PadraoAntecipacao? padrao = cmd.PadraoAntecipacao is not null
                ? Enum.Parse<Domain.Common.PadraoAntecipacao>(cmd.PadraoAntecipacao, true)
                : (Domain.Common.PadraoAntecipacao?)null;

            decimal? breakFrac = cmd.BreakFundingFeePct.HasValue
                ? Domain.Common.Percentual.De(cmd.BreakFundingFeePct.Value).AsDecimal
                : (decimal?)null;

            decimal? tlaSaldoFrac = cmd.TlaPctSobreSaldo.HasValue
                ? Domain.Common.Percentual.De(cmd.TlaPctSobreSaldo.Value).AsDecimal
                : (decimal?)null;

            decimal? tlaMesFrac = cmd.TlaPctPorMesRemanescente.HasValue
                ? Domain.Common.Percentual.De(cmd.TlaPctPorMesRemanescente.Value).AsDecimal
                : (decimal?)null;

            decimal? minParcialFrac = cmd.ValorMinimoParcialPct.HasValue
                ? Domain.Common.Percentual.De(cmd.ValorMinimoParcialPct.Value).AsDecimal
                : (decimal?)null;

            limite.ConfigurarAntecipacao(
                padrao,
                breakFrac,
                tlaSaldoFrac,
                tlaMesFrac,
                minParcialFrac,
                cmd.ObservacoesAntecipacao,
                clock);
        }

        repo.Update(limite);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteBancoDto.From(limite);
    }
}
