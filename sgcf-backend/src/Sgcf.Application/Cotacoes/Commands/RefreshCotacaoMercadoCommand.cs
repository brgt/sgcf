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
    ICotacaoSpotCache spotCache,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock) : IRequestHandler<RefreshCotacaoMercadoCommand, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(RefreshCotacaoMercadoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        LocalDate hoje = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        // Refresh usa a cotação de mercado MAIS RECENTE: spot intraday (Redis) se disponível;
        // caso contrário, o fechamento PtaxD0 do dia corrente. Antes consultava PtaxD1 (D-1),
        // mas o refresh manual quer o valor de mercado atual, não o de D-1.
        decimal novoPtax;
        Money? spot = await spotCache.GetSpotAsync(Moeda.Usd, cancellationToken);
        if (spot is not null)
        {
            novoPtax = spot.Value.Valor;
        }
        else
        {
            CotacaoFx d0 = await cotacaoResolver.ResolverFxAsync(
                Moeda.Usd,
                TipoCotacao.PtaxD0,
                hoje,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Cotação USD/BRL atual não disponível (sem spot intraday nem fechamento PtaxD0 do dia). " +
                    "Cadastre a cotação USD/BRL antes de fazer o refresh.");

            novoPtax = d0.ValorVenda.Valor;
        }

        cotacao.RefreshSnapshotMercado(novoPtax, clock);
        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao);
    }
}
