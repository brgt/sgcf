using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NodaTime;
using Sgcf.Application.Simulacao.Cache;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Infrastructure.Cache.Simulacao;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Cache;

/// <summary>
/// Testes de integração para RedisCronogramaSimulacaoCache.
/// Usa Testcontainers Redis para garantir comportamento real de TTL,
/// atomicidade e serialização — sem mocks do Redis.
/// </summary>
[Trait("Category", "Slow")]
public sealed class RedisCronogramaSimulacaoCacheTests : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    // ConnectionMultiplexer (tipo concreto) para evitar CA1859
    private ConnectionMultiplexer _redis = default!;
    private RedisCronogramaSimulacaoCache _sut = default!;

    // IDs fixos reutilizados nos testes
    private static readonly Guid CenarioId   = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SimulacaoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const int Version1 = 1;
    private const int Version2 = 2;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        _sut   = CriarSut(ttlSeconds: 60);
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private RedisCronogramaSimulacaoCache CriarSut(int ttlSeconds = 60)
    {
        IOptions<CronogramaSimulacaoCacheOptions> opts =
            Options.Create(new CronogramaSimulacaoCacheOptions { TtlSeconds = ttlSeconds });
        return new RedisCronogramaSimulacaoCache(_redis, opts);
    }

    // ReadOnlyCollection<T> para evitar CA1859
    private static ReadOnlyCollection<EventoCronogramaGerado> CriarCronograma(int qtd = 3) =>
        Enumerable.Range(1, qtd)
            .Select(i => new EventoCronogramaGerado(
                NumeroEvento: i,
                Tipo:         TipoEventoCronograma.Principal,
                DataPrevista: new LocalDate(2026, (i % 12) + 1, 1),
                Valor:        new Money(1_000m * i, Moeda.Brl),
                SaldoDevedorApos: 10_000m - (1_000m * i)))
            .ToList()
            .AsReadOnly();

    // ── 1. Get em chave inexistente retorna null ──────────────────────────────

    [Fact]
    public async Task GetAsync_ChaveInexistente_RetornaNull()
    {
        // Arrange: nada foi salvo

        // Act
        IReadOnlyList<EventoCronogramaGerado>? resultado =
            await _sut.GetAsync(CenarioId, SimulacaoId, Version1);

        // Assert
        resultado.Should().BeNull();
    }

    // ── 2. Set + Get round-trip devolve lista de eventos idêntica ─────────────

    [Fact]
    public async Task SetAsync_E_GetAsync_RoundTrip_DevolveListaIdentica()
    {
        // Arrange
        ReadOnlyCollection<EventoCronogramaGerado> original = CriarCronograma(5);

        // Act
        await _sut.SetAsync(CenarioId, SimulacaoId, Version1, original);
        IReadOnlyList<EventoCronogramaGerado>? recuperado =
            await _sut.GetAsync(CenarioId, SimulacaoId, Version1);

        // Assert
        recuperado.Should().NotBeNull();
        recuperado.Should().HaveCount(5);
        for (int i = 0; i < original.Count; i++)
        {
            recuperado![i].NumeroEvento.Should().Be(original[i].NumeroEvento);
            recuperado[i].Tipo.Should().Be(original[i].Tipo);
            recuperado[i].DataPrevista.Should().Be(original[i].DataPrevista);
            recuperado[i].Valor.Valor.Should().Be(original[i].Valor.Valor);
            recuperado[i].Valor.Moeda.Should().Be(original[i].Valor.Moeda);
            recuperado[i].SaldoDevedorApos.Should().Be(original[i].SaldoDevedorApos);
        }
    }

    // ── 3. TTL curto: chave expira após TTL ───────────────────────────────────

    [Fact]
    public async Task SetAsync_ComTtlCurto_ChaveExpiraAposTtl()
    {
        // Arrange: SUT com TTL de 1 segundo para o teste ser rápido
        RedisCronogramaSimulacaoCache sutCurto = CriarSut(ttlSeconds: 1);
        ReadOnlyCollection<EventoCronogramaGerado> cronograma = CriarCronograma(2);

        // Act
        await sutCurto.SetAsync(CenarioId, SimulacaoId, Version1, cronograma);

        // Imediatamente disponível
        IReadOnlyList<EventoCronogramaGerado>? antes =
            await sutCurto.GetAsync(CenarioId, SimulacaoId, Version1);
        antes.Should().NotBeNull("deve existir logo após SetAsync");

        // Aguarda a expiração real do Redis
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Depois do TTL: chave não existe mais
        IReadOnlyList<EventoCronogramaGerado>? depois =
            await sutCurto.GetAsync(CenarioId, SimulacaoId, Version1);
        depois.Should().BeNull("a chave deve ter expirado após o TTL");
    }

    // ── 4. Version diferente produz chave diferente ───────────────────────────

    [Fact]
    public async Task SetAsync_ComVersionsDiferentes_ChavesSaoIndependentes()
    {
        // Arrange
        ReadOnlyCollection<EventoCronogramaGerado> v1 = CriarCronograma(2);
        ReadOnlyCollection<EventoCronogramaGerado> v2 = CriarCronograma(4);

        // Act: salva duas versões distintas
        await _sut.SetAsync(CenarioId, SimulacaoId, Version1, v1);
        await _sut.SetAsync(CenarioId, SimulacaoId, Version2, v2);

        // Assert: cada versão retorna sua própria lista
        IReadOnlyList<EventoCronogramaGerado>? resultV1 =
            await _sut.GetAsync(CenarioId, SimulacaoId, Version1);
        IReadOnlyList<EventoCronogramaGerado>? resultV2 =
            await _sut.GetAsync(CenarioId, SimulacaoId, Version2);

        resultV1.Should().NotBeNull().And.HaveCount(2);
        resultV2.Should().NotBeNull().And.HaveCount(4);
    }

    // ── 5. InvalidarPorSimulacaoAsync remove todas as versões ─────────────────

    [Fact]
    public async Task InvalidarPorSimulacaoAsync_RemoveTodasAsVersoes()
    {
        // Arrange: salva versões 1, 2 e 3
        for (int v = 1; v <= 3; v++)
        {
            await _sut.SetAsync(CenarioId, SimulacaoId, v, CriarCronograma(v));
        }

        // Act
        await _sut.InvalidarPorSimulacaoAsync(CenarioId, SimulacaoId);

        // Assert: nenhuma versão deve existir
        for (int v = 1; v <= 3; v++)
        {
            IReadOnlyList<EventoCronogramaGerado>? resultado =
                await _sut.GetAsync(CenarioId, SimulacaoId, v);
            resultado.Should().BeNull($"versão {v} deveria ter sido invalidada");
        }
    }

    // ── 6. InvalidarPorSimulacaoAsync não afeta outras simulações ─────────────

    [Fact]
    public async Task InvalidarPorSimulacaoAsync_NaoAfetaOutrasSimulacoes()
    {
        // Arrange
        Guid outraSimulacaoId = Guid.NewGuid();
        await _sut.SetAsync(CenarioId, SimulacaoId,      Version1, CriarCronograma(2));
        await _sut.SetAsync(CenarioId, outraSimulacaoId, Version1, CriarCronograma(3));

        // Act: invalida apenas a primeira simulação
        await _sut.InvalidarPorSimulacaoAsync(CenarioId, SimulacaoId);

        // Assert: a outra simulação permanece intacta
        IReadOnlyList<EventoCronogramaGerado>? outra =
            await _sut.GetAsync(CenarioId, outraSimulacaoId, Version1);
        outra.Should().NotBeNull("a outra simulação não deve ser afetada pela invalidação");
        outra.Should().HaveCount(3);
    }

    // ── 7. Serialização correta de LocalDate (NodaTime) ──────────────────────

    [Fact]
    public async Task SetAsync_SerializaEventoComLocalDateCorretamente()
    {
        // Arrange: datas específicas que devem ser preservadas bit-a-bit
        LocalDate data1 = new(2026, 12, 31);
        LocalDate data2 = new(2027,  1,  1);

        IReadOnlyList<EventoCronogramaGerado> cronograma =
        [
            new EventoCronogramaGerado(1, TipoEventoCronograma.Principal, data1, new Money(500m, Moeda.Brl), 9_500m),
            new EventoCronogramaGerado(2, TipoEventoCronograma.Juros,     data2, new Money(100m, Moeda.Brl), null),
        ];

        // Act
        await _sut.SetAsync(CenarioId, SimulacaoId, Version1, cronograma);
        IReadOnlyList<EventoCronogramaGerado>? recuperado =
            await _sut.GetAsync(CenarioId, SimulacaoId, Version1);

        // Assert: datas idênticas após round-trip
        recuperado.Should().NotBeNull();
        recuperado![0].DataPrevista.Should().Be(data1);
        recuperado[1].DataPrevista.Should().Be(data2);
    }

    // ── 8. Concorrência: dois Gets simultâneos não corrompem dados ────────────

    [Fact]
    public async Task GetAsync_DoisGetsSimultaneos_NaoCorrompemDados()
    {
        // Arrange
        ReadOnlyCollection<EventoCronogramaGerado> original = CriarCronograma(10);
        await _sut.SetAsync(CenarioId, SimulacaoId, Version1, original);

        // Act: dispara dois Gets em paralelo
        Task<IReadOnlyList<EventoCronogramaGerado>?> t1 =
            _sut.GetAsync(CenarioId, SimulacaoId, Version1);
        Task<IReadOnlyList<EventoCronogramaGerado>?> t2 =
            _sut.GetAsync(CenarioId, SimulacaoId, Version1);

        IReadOnlyList<EventoCronogramaGerado>?[] resultados = await Task.WhenAll(t1, t2);

        // Assert: ambos retornam a mesma lista completa
        resultados[0].Should().NotBeNull().And.HaveCount(10);
        resultados[1].Should().NotBeNull().And.HaveCount(10);
        resultados[0]![0].NumeroEvento.Should().Be(resultados[1]![0].NumeroEvento);
    }

    // ── 9. GetOrCreateAsync calcula somente na primeira chamada ───────────────

    [Fact]
    public async Task GetOrCreateAsync_CalculaSomentePrimeiraVez()
    {
        // Arrange
        int chamadas = 0;

        IReadOnlyList<EventoCronogramaGerado> CronogramaFactory()
        {
            chamadas++;
            return CriarCronograma(4);
        }

        // Act: primeira chamada — factory deve ser executada
        IReadOnlyList<EventoCronogramaGerado> resultado1 =
            await _sut.GetOrCreateAsync(
                CenarioId, SimulacaoId, Version1,
                () => Task.FromResult<IReadOnlyList<EventoCronogramaGerado>>(CronogramaFactory()));

        // Segunda chamada — factory NÃO deve ser executada (cache hit)
        IReadOnlyList<EventoCronogramaGerado> resultado2 =
            await _sut.GetOrCreateAsync(
                CenarioId, SimulacaoId, Version1,
                () => Task.FromResult<IReadOnlyList<EventoCronogramaGerado>>(CronogramaFactory()));

        // Assert
        chamadas.Should().Be(1, "a factory só deve ser chamada no cache miss");
        resultado1.Should().HaveCount(4);
        resultado2.Should().HaveCount(4);
        resultado2[0].NumeroEvento.Should().Be(resultado1[0].NumeroEvento);
    }

    // ── 10. GetOrCreateAsync com versões diferentes chama factory em cada miss ─

    [Fact]
    public async Task GetOrCreateAsync_VersoesDistintas_CalculaCadaUmaUmaVez()
    {
        // Arrange
        int chamadasV1 = 0;
        int chamadasV2 = 0;

        // Act — primeira rodada: ambas as versões são miss
        await _sut.GetOrCreateAsync(CenarioId, SimulacaoId, Version1,
            () => { chamadasV1++; return Task.FromResult(CriarCronograma(2) as IReadOnlyList<EventoCronogramaGerado>); });

        await _sut.GetOrCreateAsync(CenarioId, SimulacaoId, Version2,
            () => { chamadasV2++; return Task.FromResult(CriarCronograma(3) as IReadOnlyList<EventoCronogramaGerado>); });

        // Segunda rodada — ambas devem ser hits
        await _sut.GetOrCreateAsync(CenarioId, SimulacaoId, Version1,
            () => { chamadasV1++; return Task.FromResult(CriarCronograma(2) as IReadOnlyList<EventoCronogramaGerado>); });

        await _sut.GetOrCreateAsync(CenarioId, SimulacaoId, Version2,
            () => { chamadasV2++; return Task.FromResult(CriarCronograma(3) as IReadOnlyList<EventoCronogramaGerado>); });

        // Assert: cada versão calculada exatamente uma vez
        chamadasV1.Should().Be(1, "V1 calculada uma única vez");
        chamadasV2.Should().Be(1, "V2 calculada uma única vez");
    }
}
