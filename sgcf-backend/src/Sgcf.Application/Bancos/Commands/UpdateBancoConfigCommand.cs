using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

namespace Sgcf.Application.Bancos.Commands;

public sealed record UpdateBancoConfigCommand(
    Guid Id,
    bool AceitaLiquidacaoTotal,
    bool AceitaLiquidacaoParcial,
    bool ExigeAnuenciaExpressa,
    bool ExigeParcelaInteira,
    int AvisoPrevioMinDiasUteis,
    string PadraoAntecipacao,
    decimal? ValorMinimoParcialPct,
    decimal? BreakFundingFeePct,
    decimal? TlaPctSobreSaldo,
    decimal? TlaPctPorMesRemanescente,
    string? ObservacoesAntecipacao)
    : IRequest<BancoDto>;

public sealed class UpdateBancoConfigCommandValidator : AbstractValidator<UpdateBancoConfigCommand>
{
    public UpdateBancoConfigCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.AvisoPrevioMinDiasUteis)
            .GreaterThanOrEqualTo(0);

        RuleFor(c => c.PadraoAntecipacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<Domain.Common.PadraoAntecipacao>(v, true, out _))
            .WithMessage($"PadraoAntecipacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Common.PadraoAntecipacao>())}.");

        RuleFor(c => c.ValorMinimoParcialPct)
            .GreaterThanOrEqualTo(0)
            .When(c => c.ValorMinimoParcialPct.HasValue);

        RuleFor(c => c.BreakFundingFeePct)
            .GreaterThanOrEqualTo(0)
            .When(c => c.BreakFundingFeePct.HasValue);

        RuleFor(c => c.TlaPctSobreSaldo)
            .GreaterThanOrEqualTo(0)
            .When(c => c.TlaPctSobreSaldo.HasValue);

        RuleFor(c => c.TlaPctPorMesRemanescente)
            .GreaterThanOrEqualTo(0)
            .When(c => c.TlaPctPorMesRemanescente.HasValue);
    }
}

public sealed class UpdateBancoConfigCommandHandler(IBancoRepository repo, IClock clock)
    : IRequestHandler<UpdateBancoConfigCommand, BancoDto>
{
    public async Task<BancoDto> Handle(UpdateBancoConfigCommand cmd, CancellationToken cancellationToken)
    {
        Banco banco = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco com Id '{cmd.Id}' não encontrado.");

        Domain.Common.PadraoAntecipacao padrao = Enum.Parse<Domain.Common.PadraoAntecipacao>(cmd.PadraoAntecipacao, true);

        decimal? valorMinimoParcialFrac = cmd.ValorMinimoParcialPct.HasValue
            ? Percentual.De(cmd.ValorMinimoParcialPct.Value).AsDecimal
            : (decimal?)null;

        decimal? breakFundingFeeFrac = cmd.BreakFundingFeePct.HasValue
            ? Percentual.De(cmd.BreakFundingFeePct.Value).AsDecimal
            : (decimal?)null;

        decimal? tlaPctSobreSaldoFrac = cmd.TlaPctSobreSaldo.HasValue
            ? Percentual.De(cmd.TlaPctSobreSaldo.Value).AsDecimal
            : (decimal?)null;

        decimal? tlaPctPorMesRemanescenteFrac = cmd.TlaPctPorMesRemanescente.HasValue
            ? Percentual.De(cmd.TlaPctPorMesRemanescente.Value).AsDecimal
            : (decimal?)null;

        banco.AtualizarConfigAntecipacao(
            cmd.AceitaLiquidacaoTotal,
            cmd.AceitaLiquidacaoParcial,
            cmd.ExigeAnuenciaExpressa,
            cmd.ExigeParcelaInteira,
            cmd.AvisoPrevioMinDiasUteis,
            valorMinimoParcialFrac,
            padrao,
            breakFundingFeeFrac,
            tlaPctSobreSaldoFrac,
            tlaPctPorMesRemanescenteFrac,
            cmd.ObservacoesAntecipacao,
            clock);

        await repo.SaveChangesAsync(cancellationToken);
        return BancoDto.From(banco);
    }
}
