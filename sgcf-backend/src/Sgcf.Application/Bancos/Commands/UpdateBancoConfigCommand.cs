using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Commands;

public sealed record UpdateBancoConfigCommand(
    Guid Id,
    bool AceitaLiquidacaoTotal,
    bool AceitaLiquidacaoParcial,
    bool ExigeAnuenciaExpressa,
    bool ExigeParcelaInteira,
    int AvisoPrevioMinDiasUteis)
    : IRequest<BancoDto>;

public sealed class UpdateBancoConfigCommandValidator : AbstractValidator<UpdateBancoConfigCommand>
{
    public UpdateBancoConfigCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.AvisoPrevioMinDiasUteis)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateBancoConfigCommandHandler(IBancoRepository repo, IClock clock)
    : IRequestHandler<UpdateBancoConfigCommand, BancoDto>
{
    public async Task<BancoDto> Handle(UpdateBancoConfigCommand cmd, CancellationToken cancellationToken)
    {
        Banco banco = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco com Id '{cmd.Id}' não encontrado.");

        banco.AtualizarConfigAntecipacao(
            cmd.AceitaLiquidacaoTotal,
            cmd.AceitaLiquidacaoParcial,
            cmd.ExigeAnuenciaExpressa,
            cmd.ExigeParcelaInteira,
            cmd.AvisoPrevioMinDiasUteis,
            clock);

        await repo.SaveChangesAsync(cancellationToken);
        repo.InvalidarCache(cmd.Id);
        return BancoDto.From(banco);
    }
}
