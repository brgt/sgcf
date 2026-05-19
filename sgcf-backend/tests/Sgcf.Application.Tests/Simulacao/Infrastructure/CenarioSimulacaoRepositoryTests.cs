using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Simulacao;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Infrastructure;

[Trait("Category", "Slow")]
[Collection("SimulacaoDb")]
public sealed class CenarioSimulacaoRepositoryTests(SimulacaoDbFixture fixture)
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private CenarioSimulacaoRepository CreateRepo() =>
        new(fixture.Context);

    /// <summary>
    /// Constrói um CenarioSimulacao em Rascunho com dados mínimos válidos.
    /// </summary>
    private CenarioSimulacao CriarCenario(string nome = "Realista 2026", int anoBase = 2026) =>
        CenarioSimulacao.Criar(
            nome: nome,
            anoBase: anoBase,
            criadoPor: "user-teste@empresa.com",
            clock: fixture.Clock);

    /// <summary>
    /// Constrói uma SimulacaoContratacao com invariantes satisfeitos para anoBase 2026.
    /// DataContratacaoPrevista = 2026-07-01 (futura ao clock fixo em 2026-06-01).
    /// </summary>
    private SimulacaoContratacao CriarSimulacao(Guid cenarioId) =>
        SimulacaoContratacao.Criar(
            cenarioId: cenarioId,
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 1),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(2.5m),
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Bullet,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: "Aval dos sócios",
            observacoes: "Obs de teste",
            clock: fixture.Clock,
            anoBase: 2026);

    // ── Teste 1: round-trip Add + GetById ────────────────────────────────────

    [Fact]
    public async Task Add_E_GetById_RetornaCenarioComSimulacoes()
    {
        // Arrange
        CenarioSimulacao cenario = CriarCenario("Add RoundTrip");
        cenario.AdicionarSimulacao(CriarSimulacao(cenario.Id), fixture.Clock);

        // Act
        CenarioSimulacaoRepository repo = CreateRepo();
        repo.Add(cenario);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo2 = new(ctx2);
        CenarioSimulacao? encontrado = await repo2.GetByIdAsync(cenario.Id);

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Nome.Should().Be("Add RoundTrip");
        encontrado.AnoBase.Should().Be(2026);
        encontrado.Status.Should().Be(StatusCenarioSimulacao.Rascunho);
        encontrado.CriadoPor.Should().Be("user-teste@empresa.com");
        encontrado.DeletedAt.Should().BeNull();
        encontrado.Simulacoes.Should().HaveCount(1);
    }

    // ── Teste 2: Version e GarantiaExigidaPrevista preservados ───────────────

    [Fact]
    public async Task Add_PreservaVersion_eGarantiaExigidaPrevista_dasSimulacoes()
    {
        // Arrange
        CenarioSimulacao cenario = CriarCenario("Version Garantia");
        SimulacaoContratacao sim = CriarSimulacao(cenario.Id);
        cenario.AdicionarSimulacao(sim, fixture.Clock);

        // Act
        CenarioSimulacaoRepository repo = CreateRepo();
        repo.Add(cenario);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo2 = new(ctx2);
        CenarioSimulacao? encontrado = await repo2.GetByIdAsync(cenario.Id);

        // Assert
        SimulacaoContratacao simRecuperada = encontrado!.Simulacoes.Single();
        simRecuperada.Version.Should().Be(1);
        simRecuperada.GarantiaExigidaPrevista.Should().Be("Aval dos sócios");
        simRecuperada.ValorPrincipal.Valor.Should().Be(1_000_000m);
        simRecuperada.ValorPrincipal.Moeda.Should().Be(Moeda.Brl);
        simRecuperada.SpreadAa!.Value.AsDecimal.Should().BeApproximately(0.025m, 0.000001m);
    }

    // ── Teste 3: Update — campos + simulações adicionadas ────────────────────

    [Fact]
    public async Task Update_AtualizaCampos_inclusiveSimulacoesAdicionadas()
    {
        // Arrange — persiste cenário inicial
        CenarioSimulacao cenario = CriarCenario("Update Inicial");
        CenarioSimulacaoRepository repo = CreateRepo();
        repo.Add(cenario);
        await repo.SaveChangesAsync();

        // Arrange — carrega em contexto fresco, modifica e persiste
        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo2 = new(ctx2);
        CenarioSimulacao? paraAtualizar = await repo2.GetByIdAsync(cenario.Id);

        paraAtualizar!.Atualizar(nome: "Update Alterado", descricao: "desc nova", anoBase: null, fixture.Clock);
        paraAtualizar.AdicionarSimulacao(CriarSimulacao(paraAtualizar.Id), fixture.Clock);
        repo2.Update(paraAtualizar);
        await repo2.SaveChangesAsync();

        // Assert — leitura em terceiro contexto
        await using SgcfDbContext ctx3 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo3 = new(ctx3);
        CenarioSimulacao? atualizado = await repo3.GetByIdAsync(cenario.Id);

        atualizado.Should().NotBeNull();
        atualizado!.Nome.Should().Be("Update Alterado");
        atualizado.Descricao.Should().Be("desc nova");
        atualizado.Simulacoes.Should().HaveCount(1);
    }

    // ── Teste 4: soft delete — não aparece no List ────────────────────────────

    [Fact]
    public async Task Remove_SoftDelete_NaoApareceNoList()
    {
        // Arrange
        CenarioSimulacao cenario = CriarCenario("SoftDelete Test");
        CenarioSimulacaoRepository repo = CreateRepo();
        repo.Add(cenario);
        await repo.SaveChangesAsync();

        // Act — soft delete via método de domínio + Update
        cenario.Deletar(fixture.Clock);
        repo.Update(cenario);
        await repo.SaveChangesAsync();

        // Assert — List filtra deletados; GetById também deve retornar null
        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo2 = new(ctx2);

        IReadOnlyList<CenarioSimulacao> lista = await repo2.ListAsync(null, null, null);
        lista.Should().NotContain(c => c.Id == cenario.Id);

        CenarioSimulacao? naoBuscavel = await repo2.GetByIdAsync(cenario.Id);
        naoBuscavel.Should().BeNull("query filter deve ocultar cenários soft-deletados");
    }

    // ── Teste 5: List com filtros ─────────────────────────────────────────────

    [Fact]
    public async Task List_FiltraPorStatus_PorAnoBase_PorCriadoPor()
    {
        // Arrange — cria dois cenários com perfis distintos
        Guid uniqueSuffix = Guid.NewGuid();
        string criadoPorFiltrado = $"filtro-{uniqueSuffix}@empresa.com";

        CenarioSimulacao cenarioAtivo = CenarioSimulacao.Criar(
            nome: "Ativo Filtro",
            anoBase: 2027,
            criadoPor: criadoPorFiltrado,
            clock: fixture.Clock);
        cenarioAtivo.Ativar(fixture.Clock);

        CenarioSimulacao cenarioRascunho = CenarioSimulacao.Criar(
            nome: "Rascunho Filtro",
            anoBase: 2028,
            criadoPor: criadoPorFiltrado,
            clock: fixture.Clock);

        CenarioSimulacao cenarioOutroUsuario = CenarioSimulacao.Criar(
            nome: "Outro Usuario",
            anoBase: 2027,
            criadoPor: "outro@empresa.com",
            clock: fixture.Clock);
        cenarioOutroUsuario.Ativar(fixture.Clock);

        CenarioSimulacaoRepository repo = CreateRepo();
        repo.Add(cenarioAtivo);
        repo.Add(cenarioRascunho);
        repo.Add(cenarioOutroUsuario);
        await repo.SaveChangesAsync();

        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo2 = new(ctx2);

        // Act + Assert: filtro por status
        IReadOnlyList<CenarioSimulacao> soAtivos = await repo2.ListAsync(StatusCenarioSimulacao.Ativo, null, null);
        soAtivos.Should().Contain(c => c.Id == cenarioAtivo.Id);
        soAtivos.Should().NotContain(c => c.Id == cenarioRascunho.Id);

        // Act + Assert: filtro por anoBase
        IReadOnlyList<CenarioSimulacao> soAno2027 = await repo2.ListAsync(null, 2027, null);
        soAno2027.Should().Contain(c => c.Id == cenarioAtivo.Id);
        soAno2027.Should().NotContain(c => c.Id == cenarioRascunho.Id);

        // Act + Assert: filtro por criadoPor
        IReadOnlyList<CenarioSimulacao> soCriadoPorFiltrado = await repo2.ListAsync(null, null, criadoPorFiltrado);
        soCriadoPorFiltrado.Should().Contain(c => c.Id == cenarioAtivo.Id);
        soCriadoPorFiltrado.Should().Contain(c => c.Id == cenarioRascunho.Id);
        soCriadoPorFiltrado.Should().NotContain(c => c.Id == cenarioOutroUsuario.Id);
    }

    // ── Teste 6: DuplicarComoRascunho persiste ambos cenários ────────────────

    [Fact]
    public async Task Update_AposDuplicarComoRascunho_PersistAmbosCenarios()
    {
        // Arrange — cria cenário original com simulação
        CenarioSimulacao original = CriarCenario("Original para Duplicar");
        original.AdicionarSimulacao(CriarSimulacao(original.Id), fixture.Clock);
        original.Ativar(fixture.Clock);

        CenarioSimulacaoRepository repo = CreateRepo();
        repo.Add(original);
        await repo.SaveChangesAsync();

        // Act — duplica em contexto fresco e persiste a cópia
        await using SgcfDbContext ctx2 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo2 = new(ctx2);
        CenarioSimulacao? originalRecuperado = await repo2.GetByIdAsync(original.Id);

        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(
            originalRecuperado!,
            novoCriadoPor: "outro-user@empresa.com",
            clock: fixture.Clock);

        repo2.Add(copia);
        await repo2.SaveChangesAsync();

        // Assert — ambos existem com dados corretos
        await using SgcfDbContext ctx3 = fixture.CreateFreshContext();
        CenarioSimulacaoRepository repo3 = new(ctx3);

        CenarioSimulacao? originalFinal = await repo3.GetByIdAsync(original.Id);
        CenarioSimulacao? copiaFinal = await repo3.GetByIdAsync(copia.Id);

        originalFinal.Should().NotBeNull();
        originalFinal!.Status.Should().Be(StatusCenarioSimulacao.Ativo);
        originalFinal.Simulacoes.Should().HaveCount(1);

        copiaFinal.Should().NotBeNull();
        copiaFinal!.Nome.Should().Be("Original para Duplicar (cópia)");
        copiaFinal.Status.Should().Be(StatusCenarioSimulacao.Rascunho);
        copiaFinal.CriadoPor.Should().Be("outro-user@empresa.com");
        copiaFinal.Simulacoes.Should().HaveCount(1);

        // Version na cópia deve ser 1 (resetado pela factory DuplicarComoRascunho)
        copiaFinal.Simulacoes.Single().Version.Should().Be(1);
    }
}
