using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Re-busca PTAX atual e invalida cache de CET em todas as propostas da cotação.
/// Apenas em EmCaptacao ou Comparada. SPEC §6.1.
/// </summary>
public sealed record RefreshCotacaoMercadoCommand(Guid CotacaoId) : IRequest<CotacaoDto>;

public sealed class RefreshCotacaoMercadoCommandValidator : AbstractValidator<RefreshCotacaoMercadoCommand>
{
    public RefreshCotacaoMercadoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
    }
}

public sealed class RefreshCotacaoMercadoCommandHandler(
    ICotacaoRepository repo,
    ICotacaoFxRepository fxRepo,
    IClock clock) : IRequestHandler<RefreshCotacaoMercadoCommand, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(RefreshCotacaoMercadoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        LocalDate hoje = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        CotacaoFx novaFx = await fxRepo.GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            hoje,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "PTAX atualizada não disponível. Cadastre a cotação USD/BRL antes de fazer o refresh.");

        cotacao.RefreshSnapshotMercado(novaFx.ValorVenda.Valor, clock);
        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao);
    }
}
