using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Simulacao.Dtos;

using NodaTime;

namespace Sgcf.Mcp.Tests.Tools;

/// <summary>
/// Fábrica de DTOs compartilhada entre SimulacaoToolsTests e SimulacaoToolsAuthTests.
/// </summary>
internal static class SimulacaoToolsTestHelpers
{
    internal static QuadroDividaDto CriarQuadroDividaDto(Guid? cenarioId = null) =>
        new(
            Ano: 2026,
            DataReferencia: new DateOnly(2026, 5, 19),
            SnapshotInicial: new SaldoPorBancoAtualDto(
                Bancos: new List<SaldoBancoAtualDto>().AsReadOnly(),
                SaldoTotalBrl: 0m,
                DataReferencia: LocalDate.FromDateTime(DateTime.UtcNow)),
            Projecao: new QuadroDividaProjecaoDto(
                Meses: new List<MesProjecaoDto>().AsReadOnly()),
            Sumario: new QuadroDividaSumarioDto(
                SaldoTotalInicioAno: 10_000_000m,
                SaldoTotalFimAno: 8_000_000m,
                TotalAmortizacaoNoAno: 2_000_000m,
                TotalCaptacaoNoAno: 0m,
                VariacaoAnualPercentual: -20m),
            Alertas: new List<string>().AsReadOnly(),
            CenarioAplicado: cenarioId.HasValue
                ? new CenarioAplicadoDto(cenarioId.Value, "Realista 2026", "Ativo", 2026, 3)
                : null);

    internal static CenarioSimulacaoDto CriarCenarioDto(Guid? id = null) =>
        new(
            Id: id ?? Guid.NewGuid(),
            Nome: "Cenário Completo",
            Descricao: "Cenário de teste",
            AnoBase: 2026,
            Status: "Ativo",
            CriadoPor: "usuario@teste.com",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Simulacoes: new List<SimulacaoContratacaoDto>().AsReadOnly());
}
