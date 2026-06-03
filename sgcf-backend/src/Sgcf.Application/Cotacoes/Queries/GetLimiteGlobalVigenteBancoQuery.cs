using MediatR;
using NodaTime;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Retorna o limite global vigente de um banco, com valores computados de utilização e disponibilidade.
/// O regime (GlobalPuro vs. PerModalidade) é detectado em tempo de consulta via
/// <see cref="IConsultaSaldoBanco.BancoEmRegimePerModalityAsync"/>.
/// Lança <see cref="KeyNotFoundException"/> quando não existe limite vigente para o banco.
/// SPEC §3.2 — Queries de LimiteGlobalBanco.
/// </summary>
public sealed record GetLimiteGlobalVigenteBancoQuery(Guid BancoId) : IRequest<LimiteGlobalBancoVigenteDto>;

public sealed class GetLimiteGlobalVigenteBancoQueryHandler(
    ILimiteGlobalBancoRepository repo,
    IConsultaSaldoBanco saldo,
    ITenantContext tenantContext,
    IClock clock)
    : IRequestHandler<GetLimiteGlobalVigenteBancoQuery, LimiteGlobalBancoVigenteDto>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<LimiteGlobalBancoVigenteDto> Handle(
        GetLimiteGlobalVigenteBancoQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        LimiteGlobalBanco limite = await repo.GetVigenteByBancoAsync(query.BancoId, hoje, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Nenhum LimiteGlobalBanco vigente encontrado para o banco {query.BancoId}.");

        Guid tenantId = tenantContext.TenantId;

        bool isPerModalidade = await saldo.BancoEmRegimePerModalityAsync(
            query.BancoId, tenantId, cancellationToken);

        // Cenário B (PerModalidade): soma de LimiteBanco.ValorUtilizadoBrl das modalidades vigentes.
        // Cenário A (GlobalPuro): soma do saldo devedor dos contratos ativos do banco.
        var valorUtilizado = isPerModalidade
            ? await saldo.CalcularUtilizadoAgregadoModalidadesAsync(query.BancoId, tenantId, cancellationToken)
            : await saldo.CalcularSaldoDevedorBancoAsync(query.BancoId, tenantId, cancellationToken);

        return LimiteGlobalBancoVigenteDto.From(limite, valorUtilizado.Valor, isPerModalidade);
    }
}
