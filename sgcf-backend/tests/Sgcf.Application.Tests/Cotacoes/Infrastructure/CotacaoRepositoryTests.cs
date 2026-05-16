using FluentAssertions;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Infrastructure;

[Trait("Category", "Slow")]
[Collection("CotacoesDb")]
public sealed class CotacaoRepositoryTests(CotacoesDbFixture fixture)
{
    private CotacaoRepository CreateRepo() => new(fixture.Context);

    private Cotacao CriarCotacao(string codigoInterno = "COT-2026-00001")
    {
        return Cotacao.Criar(
            codigoInterno: codigoInterno,
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 5, 16),
            dataPtaxReferencia: new LocalDate(2026, 5, 15),
            ptaxUsadaUsdBrl: 5.20m,
            clock: fixture.Clock);
    }

    [Fact]
    public async Task Add_E_GetById_RetornaCotacaoCorreta()
    {
        CotacaoRepository repo = CreateRepo();
        Cotacao cotacao = CriarCotacao("COT-2026-10001");

        repo.Add(cotacao);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CotacaoRepository repo2 = new(ctx2);
        Cotacao? encontrada = await repo2.GetByIdAsync(cotacao.Id);

        encontrada.Should().NotBeNull();
        encontrada!.CodigoInterno.Should().Be("COT-2026-10001");
        encontrada.Status.Should().Be(StatusCotacao.Rascunho);
        encontrada.ValorAlvoBrl.Valor.Should().Be(500_000m);
    }

    [Fact]
    public async Task GetByIdWithPropostas_CarregaPropostas()
    {
        CotacaoRepository repo = CreateRepo();
        Cotacao cotacao = CriarCotacao("COT-2026-10002");
        cotacao.Enviar(fixture.Clock);
        cotacao.AdicionarBancoAlvo(Guid.NewGuid());

        repo.Add(cotacao);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CotacaoRepository repo2 = new(ctx2);
        Cotacao? comPropostas = await repo2.GetByIdWithPropostasAsync(cotacao.Id);

        comPropostas.Should().NotBeNull();
        comPropostas!.Propostas.Should().BeEmpty(); // sem propostas ainda
        comPropostas.Status.Should().Be(StatusCotacao.EmCaptacao);
    }

    [Fact]
    public async Task ListPagedAsync_FiltroPorStatus_RetornaSomenteCorretos()
    {
        CotacaoRepository repo = CreateRepo();

        Cotacao rascunho = CriarCotacao("COT-2026-10003");
        Cotacao emCaptacao = CriarCotacao("COT-2026-10004");
        emCaptacao.Enviar(fixture.Clock);

        repo.Add(rascunho);
        repo.Add(emCaptacao);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CotacaoRepository repo2 = new(ctx2);
        Application.Cotacoes.CotacaoFilter filtro = new(Status: StatusCotacao.Rascunho);
        (IReadOnlyList<Cotacao> items, int total) = await repo2.ListPagedAsync(filtro, 1, 20);

        items.Should().OnlyContain(c => c.Status == StatusCotacao.Rascunho);
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GerarProximoCodigoInterno_RetornaSequencial()
    {
        CotacaoRepository repo = CreateRepo();

        string cod1 = await repo.GerarProximoCodigoInternoAsync(2026);
        // O número pode variar dependendo dos testes anteriores; apenas valida o formato.
        cod1.Should().StartWith("COT-2026-");
        cod1.Length.Should().Be("COT-2026-00001".Length);
    }

    [Fact]
    public async Task GerarProximoCodigoInterno_ComExistentes_IncrementaCorretamente()
    {
        CotacaoRepository repo = CreateRepo();

        // Insere cotação com código conhecido para garantir sequência
        Cotacao cotacaoBase = CriarCotacao("COT-2099-00050");
        repo.Add(cotacaoBase);
        await repo.SaveChangesAsync();

        string proximo = await repo.GerarProximoCodigoInternoAsync(2099);
        proximo.Should().Be("COT-2099-00051");
    }

    [Fact]
    public async Task SoftDelete_NaoRetornaCotacaoDeletada()
    {
        CotacaoRepository repo = CreateRepo();
        Cotacao cotacao = CriarCotacao("COT-2026-10005");

        repo.Add(cotacao);
        await repo.SaveChangesAsync();

        // Soft delete via domínio
        cotacao.Deletar(fixture.Clock);
        repo.Update(cotacao);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CotacaoRepository repo2 = new(ctx2);
        Cotacao? deletada = await repo2.GetByIdAsync(cotacao.Id);

        deletada.Should().BeNull(); // query filter exclui deletadas
    }
}
