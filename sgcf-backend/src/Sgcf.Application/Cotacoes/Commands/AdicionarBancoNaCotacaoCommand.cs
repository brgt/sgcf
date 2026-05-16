using FluentValidation;
using MediatR;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Adiciona banco-alvo à cotação. Valida limite disponível antes de adicionar.
/// SPEC §3.2 regra 8, §6.1.
/// </summary>
public sealed record AdicionarBancoNaCotacaoCommand(
    Guid CotacaoId,
    Guid BancoId) : IRequest<Unit>;

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
    ILimiteBancoRepository limiteRepo) : IRequestHandler<AdicionarBancoNaCotacaoCommand, Unit>
{
    public async Task<Unit> Handle(AdicionarBancoNaCotacaoCommand cmd, CancellationToken cancellationToken)
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

        return Unit.Value;
    }
}
