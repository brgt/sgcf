using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Xunit;

namespace Sgcf.Application.Tests.Bancos;

/// <summary>
/// Testes unitários para a lógica de cache do decorator <c>CachedBancoRepository</c>.
///
/// Como <c>BancoRepository</c> é <c>internal sealed</c> e exige um <c>DbContext</c> real,
/// testamos o comportamento de cache via um wrapper de teste (<see cref="CachedBancoRepositoryTestWrapper"/>)
/// que replica exatamente a mesma lógica do decorator de produção.
/// O inner é um mock de <see cref="IBancoRepository"/> e o cache é um <see cref="IMemoryCache"/> real.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CachedBancoRepositoryTests
{
    // ── Wrapper que simula CachedBancoRepository com inner mockado ─────────────

    /// <summary>
    /// Simula o comportamento do <see cref="CachedBancoRepository"/> sem depender do tipo concreto
    /// <see cref="BancoRepository"/> — replica a lógica de cache para fins de teste.
    ///
    /// Esta abordagem é necessária porque <see cref="BancoRepository"/> é internal sealed
    /// e não pode ser instanciado em testes de unidade sem um DbContext real.
    /// </summary>
    private sealed class CachedBancoRepositoryTestWrapper(IBancoRepository inner, IMemoryCache cache) : IBancoRepository
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private static string Chave(Guid id) => $"banco:{id:N}";

        public Task<Banco?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            cache.GetOrCreateAsync(Chave(id), entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Ttl;
                return inner.GetByIdAsync(id, ct);
            })!;

        public void InvalidarCache(Guid id) => cache.Remove(Chave(id));

        public Task<Banco?> GetByCodigoCompeAsync(string codigoCompe, CancellationToken ct = default) =>
            inner.GetByCodigoCompeAsync(codigoCompe, ct);

        public Task<Banco?> GetByApelidoAsync(string apelido, CancellationToken ct = default) =>
            inner.GetByApelidoAsync(apelido, ct);

        public Task<IReadOnlyList<Banco>> ListAllAsync(CancellationToken ct = default) =>
            inner.ListAllAsync(ct);

        public Task<IReadOnlyList<Banco>> ListFilteredAsync(string? search, CancellationToken ct = default) =>
            inner.ListFilteredAsync(search, ct);

        public void Add(Banco banco) => inner.Add(banco);

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);

        public Task<IReadOnlyList<Banco>> ListComLimiteCreditoSetadoAsync(CancellationToken ct = default) =>
            inner.ListComLimiteCreditoSetadoAsync(ct);
    }

    // ── Setup simplificado usando o wrapper ────────────────────────────────────

    private static (IBancoRepository cached, IBancoRepository inner, IMemoryCache cache) CriarSetup()
    {
        IBancoRepository inner = Substitute.For<IBancoRepository>();

        ServiceCollection services = new();
        services.AddMemoryCache();
        IServiceProvider sp = services.BuildServiceProvider();
        IMemoryCache cache = sp.GetRequiredService<IMemoryCache>();

        IBancoRepository cached = new CachedBancoRepositoryTestWrapper(inner, cache);
        return (cached, inner, cache);
    }

    private static Banco CriarBanco() =>
        Banco.Criar("001", "Banco Teste S.A.", "BancoTeste",
            NSubstitute.Substitute.For<NodaTime.IClock>());

    // ── Testes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Primeiro acesso (cache miss): inner é chamado e o resultado é armazenado.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_CacheMiss_ChamaInner()
    {
        // Arrange
        var (cached, inner, _) = CriarSetup();
        Banco banco = CriarBanco();
        inner.GetByIdAsync(banco.Id, Arg.Any<CancellationToken>()).Returns(banco);

        // Act
        Banco? resultado = await cached.GetByIdAsync(banco.Id);

        // Assert
        resultado.Should().Be(banco);
        await inner.Received(1).GetByIdAsync(banco.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Segundo acesso ao mesmo ID (cache hit): inner não é chamado novamente.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_CacheHit_NaoChamaInner()
    {
        // Arrange
        var (cached, inner, _) = CriarSetup();
        Banco banco = CriarBanco();
        inner.GetByIdAsync(banco.Id, Arg.Any<CancellationToken>()).Returns(banco);

        // Act — dois acessos ao mesmo ID
        await cached.GetByIdAsync(banco.Id);
        await cached.GetByIdAsync(banco.Id);

        // Assert — inner chamado apenas uma vez (segundo acesso foi do cache)
        await inner.Received(1).GetByIdAsync(banco.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Após <c>InvalidarCache</c>, o próximo acesso deve chamar o inner novamente.
    /// </summary>
    [Fact]
    public async Task InvalidarCache_ProximoAcessoChamaInner()
    {
        // Arrange
        var (cached, inner, _) = CriarSetup();
        Banco banco = CriarBanco();
        inner.GetByIdAsync(banco.Id, Arg.Any<CancellationToken>()).Returns(banco);

        // Preenche o cache
        await cached.GetByIdAsync(banco.Id);
        await inner.Received(1).GetByIdAsync(banco.Id, Arg.Any<CancellationToken>());

        // Act — invalida e acessa novamente
        cached.InvalidarCache(banco.Id);
        await cached.GetByIdAsync(banco.Id);

        // Assert — inner chamado duas vezes: uma antes e uma depois da invalidação
        await inner.Received(2).GetByIdAsync(banco.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// IDs distintos têm entradas de cache independentes.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_IdsDiferentes_CachesIndependentes()
    {
        // Arrange
        var (cached, inner, _) = CriarSetup();
        Banco banco1 = CriarBanco();
        Banco banco2 = CriarBanco();
        inner.GetByIdAsync(banco1.Id, Arg.Any<CancellationToken>()).Returns(banco1);
        inner.GetByIdAsync(banco2.Id, Arg.Any<CancellationToken>()).Returns(banco2);

        // Act — acessa banco1 duas vezes e banco2 uma vez
        await cached.GetByIdAsync(banco1.Id);
        await cached.GetByIdAsync(banco1.Id);
        await cached.GetByIdAsync(banco2.Id);

        // Assert — inner chamado 1x por banco (banco1 cacheado, banco2 miss)
        await inner.Received(1).GetByIdAsync(banco1.Id, Arg.Any<CancellationToken>());
        await inner.Received(1).GetByIdAsync(banco2.Id, Arg.Any<CancellationToken>());
    }
}
