using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

[Trait("Category", "Unit")]
public sealed class TarifasIofQueryTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 12, 0);

    private static GetTarifasIofQueryHandler CriarHandler(
        IEventoCronogramaRepository eventoRepo,
        IContratoRepository contratoRepo,
        IBancoRepository? bancoRepo = null)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        bancoRepo ??= Substitute.For<IBancoRepository>();

        return new GetTarifasIofQueryHandler(eventoRepo, contratoRepo, bancoRepo, clock);
    }

    private static Contrato CriarContrato(Guid bancoId, ModalidadeContrato modalidade = ModalidadeContrato.Finimp)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return Contrato.Criar(
            numeroExterno: $"CTR-{Guid.NewGuid():N}",
            bancoId: bancoId,
            modalidade: modalidade,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 1),
            dataVencimento: new LocalDate(2027, 1, 1),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    // ── Teste 1: sem eventos retorna todos os totais como zero ──────────────────

    [Fact]
    public async Task Handle_SemEventos_RetornaTotaisZero()
    {
        // Arrange
        IEventoCronogramaRepository eventoRepo = Substitute.For<IEventoCronogramaRepository>();
        eventoRepo.ListPorTiposAsync(default!, default)
                  .ReturnsForAnyArgs(new List<EventoCronograma>().AsReadOnly());

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default).ReturnsForAnyArgs(new List<Contrato>().AsReadOnly());

        GetTarifasIofQueryHandler handler = CriarHandler(eventoRepo, contratoRepo);

        // Act
        EnvelopeResponse<TarifasIofDto> resposta =
            await handler.Handle(new GetTarifasIofQuery(), CancellationToken.None);

        // Assert — dados
        TarifasIofDto dados = resposta.Data;
        dados.TotalIofBrl.Should().Be(0m);
        dados.TotalTarifasBrl.Should().Be(0m);
        dados.TotalGeralBrl.Should().Be(0m);
        dados.PorBanco.Should().BeEmpty();
        dados.PorModalidade.Should().BeEmpty();

        // Assert — envelope
        resposta.Meta.Completude.Should().Be(Completude.Completo);
        resposta.Meta.FontesConsultadas.Should().HaveCount(1);
        resposta.Meta.FontesConsultadas[0].Fonte.Should().Be("cronograma");
        resposta.Meta.FontesConsultadas[0].Status.Should().Be("ok");
        resposta.Meta.FontesConsultadas[0].Registros.Should().Be(0);
    }

    // ── Teste 2: 1 IOF (100 BRL) + 1 tarifa (200 BRL) no mesmo contrato ────────

    [Fact]
    public async Task Handle_ComEventosIofETarifas_AgregaCorretamente()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Contrato contrato = CriarContrato(bancoId, ModalidadeContrato.Finimp);

        // Evento de IOF: 100 BRL
        EventoCronograma eventoIof = EventoCronograma.Criar(
            contratoId: contrato.Id,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.IofCambio,
            dataPrevista: new LocalDate(2026, 6, 1),
            valorMoedaOriginal: new Money(100m, Moeda.Brl));

        // Evento de tarifa: 200 BRL
        EventoCronograma eventoTarifa = EventoCronograma.Criar(
            contratoId: contrato.Id,
            numeroEvento: 2,
            tipo: TipoEventoCronograma.TarifaRof,
            dataPrevista: new LocalDate(2026, 6, 1),
            valorMoedaOriginal: new Money(200m, Moeda.Brl));

        IEventoCronogramaRepository eventoRepo = Substitute.For<IEventoCronogramaRepository>();
        eventoRepo.ListPorTiposAsync(default!, default)
                  .ReturnsForAnyArgs(new List<EventoCronograma> { eventoIof, eventoTarifa }.AsReadOnly());

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(default)
                    .ReturnsForAnyArgs(new List<Contrato> { contrato }.AsReadOnly());

        // Banco stub — o handler resolve nomes de bancos via IBancoRepository
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        Banco banco = Banco.Criar("001", "Banco Teste S.A.", "BancoTeste", clock);
        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>())
                 .Returns(banco);

        GetTarifasIofQueryHandler handler = CriarHandler(eventoRepo, contratoRepo, bancoRepo);

        // Act
        EnvelopeResponse<TarifasIofDto> resposta =
            await handler.Handle(new GetTarifasIofQuery(), CancellationToken.None);

        // Assert — totais globais
        TarifasIofDto dados = resposta.Data;
        dados.TotalIofBrl.Should().Be(100m);
        dados.TotalTarifasBrl.Should().Be(200m);
        dados.TotalGeralBrl.Should().Be(300m);

        // Assert — agregação por banco
        dados.PorBanco.Should().HaveCount(1);
        TarifasIofPorBancoDto linhaBanco = dados.PorBanco[0];
        linhaBanco.BancoId.Should().Be(bancoId);
        linhaBanco.TotalIofBrl.Should().Be(100m);
        linhaBanco.TotalTarifasBrl.Should().Be(200m);
        linhaBanco.TotalBrl.Should().Be(300m);

        // Assert — agregação por modalidade
        dados.PorModalidade.Should().HaveCount(1);
        TarifasIofPorModalidadeDto linhaModalidade = dados.PorModalidade[0];
        linhaModalidade.Modalidade.Should().Be("Finimp");
        linhaModalidade.TotalIofBrl.Should().Be(100m);
        linhaModalidade.TotalTarifasBrl.Should().Be(200m);
        linhaModalidade.TotalBrl.Should().Be(300m);

        // Assert — envelope completo (moeda BRL, sem fallback)
        resposta.Meta.Completude.Should().Be(Completude.Completo);
        resposta.Meta.FontesConsultadas[0].Registros.Should().Be(2);
    }
}
