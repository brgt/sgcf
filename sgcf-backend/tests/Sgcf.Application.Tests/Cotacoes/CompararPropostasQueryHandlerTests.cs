using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Queries;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class CompararPropostasQueryHandlerTests
{
    [Fact]
    public async Task Handle_SemPropostas_RetornaListaVazia()
    {
        // Arrange
        Cotacao cotacao = TestHelpers.CriarCotacaoComparada();

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IClock clock = TestHelpers.CriarClock();

        cotacaoRepo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        CompararPropostasQueryHandler handler = new(cotacaoRepo, cdiRepo, clock);
        CompararPropostasQuery query = new(cotacao.Id);

        // Act
        IReadOnlyList<ComparativoDto> resultado = await handler.Handle(query, default);

        // Assert
        resultado.Should().BeEmpty();
        // CDI não deve ter sido consultado — lista vazia retorna cedo
        await cdiRepo.DidNotReceive().GetMaisRecenteAsync(Arg.Any<NodaTime.LocalDate>(), default);
    }

    [Fact]
    public async Task Handle_DuasPropostas_RetornaOrdenadaPorCustoTotal()
    {
        // Arrange — proposta barata vs proposta cara
        Cotacao cotacao = TestHelpers.CriarCotacaoComparada();

        Guid bancoBarato = Guid.NewGuid();
        Guid bancoCaro = Guid.NewGuid();

        // Proposta barata: taxa 5% + spread 0.5%
        Proposta barata = cotacao.AdicionarProposta(
            bancoBarato, Sgcf.Domain.Common.Moeda.Usd,
            new Sgcf.Domain.Common.Money(100_000m, Sgcf.Domain.Common.Moeda.Usd),
            taxaAaPercentual: 5.0m, iofPercentual: 0.38m, spreadAaPercentual: 0.5m,
            prazoDias: 180,
            estruturaAmortizacao: Sgcf.Domain.Contratos.EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Sgcf.Domain.Contratos.Periodicidade.Bullet,
            exigeNdf: false, custoNdfAaPercentual: null,
            garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Sgcf.Domain.Common.Money(600_000m, Sgcf.Domain.Common.Moeda.Brl),
            garantiaEhCdbCativo: false, rendimentoCdbAaPercentual: null,
            dataCaptura: TestHelpers.DataAberturaPadrao);

        // Proposta cara: taxa 9% + spread 1%
        Proposta cara = cotacao.AdicionarProposta(
            bancoCaro, Sgcf.Domain.Common.Moeda.Usd,
            new Sgcf.Domain.Common.Money(100_000m, Sgcf.Domain.Common.Moeda.Usd),
            taxaAaPercentual: 9.0m, iofPercentual: 0.38m, spreadAaPercentual: 1.0m,
            prazoDias: 180,
            estruturaAmortizacao: Sgcf.Domain.Contratos.EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Sgcf.Domain.Contratos.Periodicidade.Bullet,
            exigeNdf: false, custoNdfAaPercentual: null,
            garantiaExigida: "Fiança",
            valorGarantiaExigidaBrl: new Sgcf.Domain.Common.Money(600_000m, Sgcf.Domain.Common.Moeda.Brl),
            garantiaEhCdbCativo: false, rendimentoCdbAaPercentual: null,
            dataCaptura: TestHelpers.DataAberturaPadrao);

        // Atualizar CET para que o handler tenha valores preenchidos
        cotacao.AtualizarCetProposta(barata.Id, 5.8m, new Sgcf.Domain.Common.Money(527_250m, Sgcf.Domain.Common.Moeda.Brl));
        cotacao.AtualizarCetProposta(cara.Id, 10.5m, new Sgcf.Domain.Common.Money(552_500m, Sgcf.Domain.Common.Moeda.Brl));

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IClock clock = TestHelpers.CriarClock();

        cotacaoRepo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);
        cdiRepo.GetMaisRecenteAsync(Arg.Any<NodaTime.LocalDate>(), default).Returns((CdiSnapshot?)null); // CDI = 0 → sem equalização

        CompararPropostasQueryHandler handler = new(cotacaoRepo, cdiRepo, clock);

        // Act
        IReadOnlyList<ComparativoDto> resultado = await handler.Handle(new CompararPropostasQuery(cotacao.Id), default);

        // Assert — 2 propostas retornadas; a barata vem primeiro
        resultado.Should().HaveCount(2);
        resultado[0].BancoId.Should().Be(bancoBarato);
        resultado[1].BancoId.Should().Be(bancoCaro);
    }

    [Fact]
    public async Task Handle_PropostaComPrazoDiferente_EqualizaViaCdiAoPrazoMaximo()
    {
        // Arrange — cotação tem prazoMaximo=180; proposta com prazo=90 deve ser equalizada
        Cotacao cotacao = TestHelpers.CriarCotacaoComparada(valorAlvoBrl: 500_000m);

        Proposta proposta90 = cotacao.AdicionarProposta(
            Guid.NewGuid(), Sgcf.Domain.Common.Moeda.Usd,
            new Sgcf.Domain.Common.Money(100_000m, Sgcf.Domain.Common.Moeda.Usd),
            taxaAaPercentual: 6.0m, iofPercentual: 0.38m, spreadAaPercentual: 0.5m,
            prazoDias: 90, // prazo diferente do máximo
            estruturaAmortizacao: Sgcf.Domain.Contratos.EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Sgcf.Domain.Contratos.Periodicidade.Bullet,
            exigeNdf: false, custoNdfAaPercentual: null,
            garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Sgcf.Domain.Common.Money(600_000m, Sgcf.Domain.Common.Moeda.Brl),
            garantiaEhCdbCativo: false, rendimentoCdbAaPercentual: null,
            dataCaptura: TestHelpers.DataAberturaPadrao);

        cotacao.AtualizarCetProposta(proposta90.Id, 7.0m, new Sgcf.Domain.Common.Money(515_000m, Sgcf.Domain.Common.Moeda.Brl));

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IClock clock = TestHelpers.CriarClock();

        cotacaoRepo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        // CDI = 12% a.a. → equalização ativa
        CdiSnapshot cdi = CdiSnapshot.Criar(TestHelpers.DataAberturaPadrao, 12.0m, TestHelpers.CriarClock());
        cdiRepo.GetMaisRecenteAsync(Arg.Any<NodaTime.LocalDate>(), default).Returns(cdi);

        CompararPropostasQueryHandler handler = new(cotacaoRepo, cdiRepo, clock);

        // Act
        IReadOnlyList<ComparativoDto> resultado = await handler.Handle(new CompararPropostasQuery(cotacao.Id), default);

        // Assert — custo total está equalizado (prazo 90 → 180)
        resultado.Should().HaveCount(1);
        // Com CDI ativo, o custo total do prazo 90 deve ser expandido ao prazo 180
        resultado[0].CustoTotalEquivalenteBrl.Should().BeGreaterThan(0m);
        resultado[0].TaxaNominalAaPercentual.Should().Be(6.0m + 0.5m);
    }

    [Fact]
    public async Task Handle_CotacaoNaoEncontrada_LancaKeyNotFoundException()
    {
        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IClock clock = TestHelpers.CriarClock();

        cotacaoRepo.GetByIdWithPropostasAsync(Arg.Any<Guid>(), default).Returns((Cotacao?)null);

        CompararPropostasQueryHandler handler = new(cotacaoRepo, cdiRepo, clock);

        Func<Task> act = () => handler.Handle(new CompararPropostasQuery(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
