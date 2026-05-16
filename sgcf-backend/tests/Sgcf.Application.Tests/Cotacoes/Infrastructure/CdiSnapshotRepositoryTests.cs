using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

[Trait("Category", "Slow")]
[Collection("CotacoesDb")]
public sealed class CdiSnapshotRepositoryTests(CotacoesDbFixture fixture)
{
    private CdiSnapshotRepository CreateRepo() => new(fixture.Context);

    private static LocalDate DataBase => new(2025, 1, 2);

    [Fact]
    public async Task Add_E_GetByData_RetornaSnapshot()
    {
        CdiSnapshotRepository repo = CreateRepo();
        LocalDate data = DataBase.PlusDays(100);
        CdiSnapshot snapshot = CdiSnapshot.Criar(data, 10.75m, fixture.Clock);

        repo.Add(snapshot);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CdiSnapshotRepository repo2 = new(ctx2);
        CdiSnapshot? encontrado = await repo2.GetByDataAsync(data);

        encontrado.Should().NotBeNull();
        encontrado!.CdiAaPercentual.Should().Be(10.75m);
    }

    [Fact]
    public async Task UniqueConstraint_PorData_LancaExcecao()
    {
        CdiSnapshotRepository repo = CreateRepo();
        LocalDate data = DataBase.PlusDays(200);

        CdiSnapshot snap1 = CdiSnapshot.Criar(data, 10.5m, fixture.Clock);
        CdiSnapshot snap2 = CdiSnapshot.Criar(data, 11.0m, fixture.Clock);

        repo.Add(snap1);
        await repo.SaveChangesAsync();

        // Segundo snapshot com mesma data deve violar o índice único
        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CdiSnapshotRepository repo2 = new(ctx2);
        repo2.Add(snap2);

        Func<Task> act = () => repo2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GetMaisRecente_RetornaUltimoAntesDaData()
    {
        CdiSnapshotRepository repo = CreateRepo();
        LocalDate base2 = DataBase.PlusDays(300);

        repo.Add(CdiSnapshot.Criar(base2, 10.0m, fixture.Clock));
        repo.Add(CdiSnapshot.Criar(base2.PlusDays(1), 10.25m, fixture.Clock));
        repo.Add(CdiSnapshot.Criar(base2.PlusDays(2), 10.5m, fixture.Clock));
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CdiSnapshotRepository repo2 = new(ctx2);
        CdiSnapshot? maisRecente = await repo2.GetMaisRecenteAsync(base2.PlusDays(1));

        maisRecente.Should().NotBeNull();
        maisRecente!.CdiAaPercentual.Should().Be(10.25m);
    }

    [Fact]
    public async Task ListByPeriodo_RetornaApenasDentroDoIntervalo()
    {
        CdiSnapshotRepository repo = CreateRepo();
        LocalDate base3 = DataBase.PlusDays(400);

        repo.Add(CdiSnapshot.Criar(base3, 9.5m, fixture.Clock));
        repo.Add(CdiSnapshot.Criar(base3.PlusDays(1), 9.75m, fixture.Clock));
        repo.Add(CdiSnapshot.Criar(base3.PlusDays(5), 10.0m, fixture.Clock)); // fora do intervalo
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CdiSnapshotRepository repo2 = new(ctx2);
        IReadOnlyList<CdiSnapshot> lista = await repo2.ListByPeriodoAsync(base3, base3.PlusDays(2));

        lista.Should().HaveCount(2);
        lista.Select(s => s.CdiAaPercentual).Should().BeEquivalentTo([9.5m, 9.75m]);
    }
}
