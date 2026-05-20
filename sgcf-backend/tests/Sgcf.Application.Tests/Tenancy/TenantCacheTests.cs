using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;
using Sgcf.Infrastructure.Tenancy;
using Xunit;

namespace Sgcf.Application.Tests.Tenancy;

/// <summary>
/// Testa TenantCache usando IMemoryCache real + Redis mock.
/// Não depende de container Docker — é fast (Category != Slow).
/// </summary>
public sealed class TenantCacheTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly StackExchange.Redis.IConnectionMultiplexer _redisMock =
        Substitute.For<StackExchange.Redis.IConnectionMultiplexer>();
    private readonly ITenantRepository _repoMock = Substitute.For<ITenantRepository>();
    private readonly TenantCache _cache;

    public TenantCacheTests()
    {
        // Configura Redis mock para retornar subscriber stub
        StackExchange.Redis.ISubscriber subscriberMock =
            Substitute.For<StackExchange.Redis.ISubscriber>();
        StackExchange.Redis.IDatabase dbMock =
            Substitute.For<StackExchange.Redis.IDatabase>();
        _redisMock.GetSubscriber(Arg.Any<object?>()).Returns(subscriberMock);
        _redisMock.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(dbMock);

        _cache = new TenantCache(_memoryCache, _redisMock);
    }

    public void Dispose() => _memoryCache.Dispose();

    [Fact]
    public async Task GetByIdAsync_TenantNaoEmCache_RetornaNull()
    {
        TenantInfo? result = await _cache.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_TenantNaoEmCache_RetornaNull()
    {
        TenantInfo? result = await _cache.GetBySlugAsync("inexistente", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_DepoisGetById_RetornaInfo()
    {
        TenantInfo info = new(Guid.NewGuid(), "proxys", StatusTenant.Ativo);

        await _cache.SetAsync(info, CancellationToken.None);
        TenantInfo? result = await _cache.GetByIdAsync(info.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(info.Id);
        result.Slug.Should().Be("proxys");
    }

    [Fact]
    public async Task SetAsync_DepoisGetBySlug_RetornaInfo()
    {
        TenantInfo info = new(Guid.NewGuid(), "proxys", StatusTenant.Ativo);

        await _cache.SetAsync(info, CancellationToken.None);
        TenantInfo? result = await _cache.GetBySlugAsync("proxys", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Slug.Should().Be("proxys");
    }

    [Fact]
    public async Task InvalidateLocal_ApagaDoMemoryCache()
    {
        TenantInfo info = new(Guid.NewGuid(), "proxys", StatusTenant.Ativo);
        await _cache.SetAsync(info, CancellationToken.None);

        _cache.InvalidateLocal(info.Id);

        TenantInfo? resultById = await _cache.GetByIdAsync(info.Id, CancellationToken.None);
        TenantInfo? resultBySlug = await _cache.GetBySlugAsync("proxys", CancellationToken.None);
        resultById.Should().BeNull();
        resultBySlug.Should().BeNull();
    }
}
