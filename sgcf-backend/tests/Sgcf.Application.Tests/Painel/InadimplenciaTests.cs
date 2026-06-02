using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Tests.Painel.Infrastructure;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

/// <summary>
/// Testes de integração para <see cref="GetInadimplenciaQueryHandler"/>.
/// Usa Testcontainers com PostgreSQL real para contratos e eventos; outras dependências são mockadas.
/// </summary>
[Trait("Category", "Slow")]
[Collection("PainelDb")]
public sealed class InadimplenciaTests(PainelDbFixture fixture)
{
    // Clock fixo: 2026-05-21 09:00 UTC → data BRT = 2026-05-21
    private static readonly LocalDate Hoje = new(2026, 5, 21);

    // ── helpers de seeding ────────────────────────────────────────────────────

    /// <summary>
    /// Persiste contratos e seus eventos atrasados no banco, devolvendo repositórios
    /// com contextos frescos (sem cache de ChangeTracker) prontos para uso pelo handler.
    /// </summary>
    private async Task<(ContratoRepository ContratoRepo, EventoCronogramaRepository CronogramaRepo)>
        SeedAsync(IEnumerable<Contrato> contratos, IEnumerable<EventoCronograma> eventosAtrasados)
    {
        await using SgcfDbContext ctxWrite = fixture.CreateFreshContext();
        ContratoRepository repoEscritaContrato = new(ctxWrite);
        EventoCronogramaRepository repoEscritaEvento = new(ctxWrite);

        foreach (Contrato contrato in contratos)
        {
            repoEscritaContrato.Add(contrato);
        }

        repoEscritaEvento.AddRange(eventosAtrasados);

        await ctxWrite.SaveChangesAsync(CancellationToken.None);

        SgcfDbContext ctxRead = fixture.CreateFreshContext();
        return (new ContratoRepository(ctxRead), new EventoCronogramaRepository(ctxRead));
    }

    /// <summary>Cria um contrato BRL ativo.</summary>
    private Contrato CriarContrato(string numeroExterno, Guid bancoId)
    {
        return Contrato.Criar(
            numeroExterno: $"{numeroExterno}-{Guid.NewGuid():N}",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 2),
            dataVencimento: new LocalDate(2028, 12, 31),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: fixture.Clock,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: new LocalDate(2028, 12, 31));
    }

    /// <summary>
    /// Cria um evento de cronograma do tipo Principal e o marca como Atrasado.
    /// O status Atrasado é o estado correto para parcelas inadimplentes.
    /// </summary>
    private static EventoCronograma CriarEventoAtrasado(
        Guid contratoId,
        LocalDate dataPrevista,
        decimal valorBrl,
        short numeroEvento = 1)
    {
        EventoCronograma evento = EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: numeroEvento,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: dataPrevista,
            valorMoedaOriginal: new Money(valorBrl, Moeda.Brl));

        evento.MarcarAtrasado();
        return evento;
    }

    /// <summary>
    /// Monta o handler com repositórios reais e dependências de FX/Banco mockadas.
    /// O banco mockado retorna lista vazia (BancoApelido ficará vazio para contratos seedados
    /// sem banco registrado, o que é aceitável para validar a lógica de agregação).
    /// </summary>
    private GetInadimplenciaQueryHandler CriarHandler(
        ContratoRepository contratoRepo,
        EventoCronogramaRepository cronogramaRepo,
        IReadOnlyList<Banco>? bancosDisponiveis = null)
    {
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
                 .Returns((Money?)null);

        IResolveTipoCotacaoService fxRepo = Substitute.For<IResolveTipoCotacaoService>();
        fxRepo.ResolverFxAsync(Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                                   Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
              .Returns((CotacaoFx?)null);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(bancosDisponiveis ?? (IReadOnlyList<Banco>)[]);

        return new GetInadimplenciaQueryHandler(
            contratoRepo,
            cronogramaRepo,
            bancoRepo,
            spotCache,
            fxRepo,
            fixture.Clock);
    }

    // ── Teste 1: 2 contratos seedados neste teste → ambos aparecem no resultado ──

    [Fact]
    public async Task Handle_DoisContratosComParcelasAtrasadas_AmbosSaoRetornados()
    {
        // Arrange — contrato A com parcela vencida há 5 dias, contrato B há 10 dias.
        // O container é compartilhado: assertemos sobre os IDs seedados aqui, não sobre
        // contagens absolutas (outros testes podem ter deixado dados no mesmo tenant).
        Guid bancoId = Guid.NewGuid();
        Contrato contratoA = CriarContrato("A", bancoId);
        Contrato contratoB = CriarContrato("B", bancoId);

        EventoCronograma eventoA = CriarEventoAtrasado(
            contratoA.Id, Hoje.PlusDays(-5), 100_000m);

        EventoCronograma eventoB = CriarEventoAtrasado(
            contratoB.Id, Hoje.PlusDays(-10), 200_000m);

        (ContratoRepository contratoRepo, EventoCronogramaRepository cronogramaRepo) =
            await SeedAsync([contratoA, contratoB], [eventoA, eventoB]);

        GetInadimplenciaQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<InadimplenciaDto> resposta =
            await handler.Handle(new GetInadimplenciaQuery(), CancellationToken.None);

        // Assert — ambos os contratos seedados devem aparecer na lista
        IEnumerable<Guid> idsRetornados = resposta.Data.Contratos.Select(c => c.ContratoId);
        idsRetornados.Should().Contain(contratoA.Id,
            "contratoA tem parcela atrasada há 5 dias");
        idsRetornados.Should().Contain(contratoB.Id,
            "contratoB tem parcela atrasada há 10 dias");
        resposta.Data.QuantidadeContratos.Should().BeGreaterThanOrEqualTo(2,
            "ao menos os dois contratos seedados devem estar contabilizados");
        resposta.Data.QuantidadeParcelas.Should().BeGreaterThanOrEqualTo(2,
            "ao menos uma parcela atrasada por contrato seedado");
    }

    // ── Teste 2: DiasAtrasoMedio é positivo quando existem parcelas atrasadas ──

    [Fact]
    public async Task Handle_ParcelasVencidasHa5e10Dias_DiasAtrasoMedioEhPositivo()
    {
        // Arrange — parcela A vencida há 5 dias, parcela B vencida há 10 dias.
        // O container é compartilhado; não podemos afirmar a média exata (outros testes
        // também seedam eventos Atrasados). Verificamos que DiasAtrasoMedio é positivo
        // e que os itens dos contratos seedados têm os dias de atraso corretos.
        Guid bancoId = Guid.NewGuid();
        Contrato contratoA = CriarContrato("MEDIA-A", bancoId);
        Contrato contratoB = CriarContrato("MEDIA-B", bancoId);

        EventoCronograma eventoA = CriarEventoAtrasado(
            contratoA.Id, Hoje.PlusDays(-5), 50_000m);

        EventoCronograma eventoB = CriarEventoAtrasado(
            contratoB.Id, Hoje.PlusDays(-10), 50_000m);

        (ContratoRepository contratoRepo, EventoCronogramaRepository cronogramaRepo) =
            await SeedAsync([contratoA, contratoB], [eventoA, eventoB]);

        GetInadimplenciaQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<InadimplenciaDto> resposta =
            await handler.Handle(new GetInadimplenciaQuery(), CancellationToken.None);

        // Assert — a média global deve ser positiva
        resposta.Data.DiasAtrasoMedio.Should().BePositive(
            "há parcelas atrasadas: DiasAtrasoMedio deve ser > 0");

        // Os itens individuais dos contratos seedados têm os dias corretos.
        InadimplenciaItemDto? itemA = resposta.Data.Contratos
            .FirstOrDefault(c => c.ContratoId == contratoA.Id);
        InadimplenciaItemDto? itemB = resposta.Data.Contratos
            .FirstOrDefault(c => c.ContratoId == contratoB.Id);

        itemA.Should().NotBeNull();
        itemB.Should().NotBeNull();
        itemA!.DiasAtrasoMaior.Should().Be(5, "eventoA venceu há 5 dias");
        itemB!.DiasAtrasoMaior.Should().Be(10, "eventoB venceu há 10 dias");
    }

    // ── Teste 3: ExposicaoTotalBrl inclui os valores dos contratos seedados ────

    [Fact]
    public async Task Handle_DoisEventosAtrasados_ExposicaoContibruidaPelosContratosSeededosEstaCorreta()
    {
        // Arrange
        // O container é compartilhado: validamos que os itens dos contratos seedados
        // têm os valores esperados, e que o total é maior ou igual a essa soma.
        Guid bancoId = Guid.NewGuid();
        Contrato contratoA = CriarContrato("EXP-A", bancoId);
        Contrato contratoB = CriarContrato("EXP-B", bancoId);

        EventoCronograma eventoA = CriarEventoAtrasado(
            contratoA.Id, Hoje.PlusDays(-5), 150_000m);

        EventoCronograma eventoB = CriarEventoAtrasado(
            contratoB.Id, Hoje.PlusDays(-10), 250_000m);

        (ContratoRepository contratoRepo, EventoCronogramaRepository cronogramaRepo) =
            await SeedAsync([contratoA, contratoB], [eventoA, eventoB]);

        GetInadimplenciaQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<InadimplenciaDto> resposta =
            await handler.Handle(new GetInadimplenciaQuery(), CancellationToken.None);

        // Assert — os itens dos contratos seedados têm a exposição correta
        InadimplenciaItemDto? itemA = resposta.Data.Contratos
            .FirstOrDefault(c => c.ContratoId == contratoA.Id);
        InadimplenciaItemDto? itemB = resposta.Data.Contratos
            .FirstOrDefault(c => c.ContratoId == contratoB.Id);

        itemA.Should().NotBeNull("contratoA foi seedado com parcela atrasada");
        itemB.Should().NotBeNull("contratoB foi seedado com parcela atrasada");

        itemA!.ExposicaoBrl.Should().Be(150_000m,
            "eventoA tem valor de 150.000 BRL");
        itemB!.ExposicaoBrl.Should().Be(250_000m,
            "eventoB tem valor de 250.000 BRL");

        // O total inclui esses dois contratos mais eventuais dados de outros testes.
        resposta.Data.ExposicaoTotalBrl.Should().BeGreaterThanOrEqualTo(400_000m,
            "ExposicaoTotalBrl deve incluir ao menos as exposições dos contratos seedados aqui");
    }

    // ── Teste 4: contrato sem parcelas atrasadas não aparece no resultado ──────

    [Fact]
    public async Task Handle_ContratoSemParcelasAtrasadas_NaoAparece()
    {
        // Arrange — contratoC apenas com evento Previsto (não Atrasado).
        // Só contratos A e B têm eventos Atrasados.
        Guid bancoId = Guid.NewGuid();
        Contrato contratoA = CriarContrato("EXCL-A", bancoId);
        Contrato contratoC = CriarContrato("EXCL-C", bancoId);   // sem atraso

        EventoCronograma eventoA = CriarEventoAtrasado(
            contratoA.Id, Hoje.PlusDays(-3), 80_000m);

        // Evento de contratoC é Previsto (futuro) — não deve entrar no painel.
        EventoCronograma eventoC = EventoCronograma.Criar(
            contratoId: contratoC.Id,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: Hoje.PlusDays(30),
            valorMoedaOriginal: new Money(90_000m, Moeda.Brl));

        (ContratoRepository contratoRepo, EventoCronogramaRepository cronogramaRepo) =
            await SeedAsync([contratoA, contratoC], [eventoA, eventoC]);

        GetInadimplenciaQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<InadimplenciaDto> resposta =
            await handler.Handle(new GetInadimplenciaQuery(), CancellationToken.None);

        // Assert — apenas contratoA deve aparecer
        IEnumerable<Guid> idsRetornados = resposta.Data.Contratos.Select(c => c.ContratoId);
        idsRetornados.Should().NotContain(contratoC.Id,
            "contrato sem parcelas atrasadas não deve aparecer no painel de inadimplência");
        idsRetornados.Should().Contain(contratoA.Id,
            "contrato com parcela atrasada deve aparecer");
    }

    // ── Teste 5: ordenação por ExposicaoBrl decrescente ──────────────────────

    [Fact]
    public async Task Handle_TresContratos_OrdenaExposicaoBrlDecrescente()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Contrato c1 = CriarContrato("ORD-1", bancoId);   // 100k
        Contrato c2 = CriarContrato("ORD-2", bancoId);   // 500k — maior
        Contrato c3 = CriarContrato("ORD-3", bancoId);   // 300k

        EventoCronograma e1 = CriarEventoAtrasado(c1.Id, Hoje.PlusDays(-2), 100_000m);
        EventoCronograma e2 = CriarEventoAtrasado(c2.Id, Hoje.PlusDays(-2), 500_000m);
        EventoCronograma e3 = CriarEventoAtrasado(c3.Id, Hoje.PlusDays(-2), 300_000m);

        (ContratoRepository contratoRepo, EventoCronogramaRepository cronogramaRepo) =
            await SeedAsync([c1, c2, c3], [e1, e2, e3]);

        GetInadimplenciaQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<InadimplenciaDto> resposta =
            await handler.Handle(new GetInadimplenciaQuery(), CancellationToken.None);

        // Assert
        resposta.Data.Contratos.Should()
            .BeInDescendingOrder(c => c.ExposicaoBrl,
                "handler ordena por ExposicaoBrl decrescente");
    }

    // ── Teste 6: envelope com Completude = Completo ───────────────────────────

    [Fact]
    public async Task Handle_MetaCompletudeCompleto()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato("META", bancoId);
        EventoCronograma evento = CriarEventoAtrasado(
            contrato.Id, Hoje.PlusDays(-1), 10_000m);

        (ContratoRepository contratoRepo, EventoCronogramaRepository cronogramaRepo) =
            await SeedAsync([contrato], [evento]);

        GetInadimplenciaQueryHandler handler = CriarHandler(contratoRepo, cronogramaRepo);

        // Act
        EnvelopeResponse<InadimplenciaDto> resposta =
            await handler.Handle(new GetInadimplenciaQuery(), CancellationToken.None);

        // Assert
        resposta.Meta.Completude.Should().Be(Completude.Completo);
    }
}
