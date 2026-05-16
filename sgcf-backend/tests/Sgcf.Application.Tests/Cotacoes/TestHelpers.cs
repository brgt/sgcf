using NodaTime;
using NSubstitute;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>Helpers compartilhados pelos testes do módulo de Cotações.</summary>
internal static class TestHelpers
{
    internal const decimal PtaxPadrao = 5.20m;
    internal static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 16, 9, 0);
    internal static readonly LocalDate DataAberturaPadrao = new(2026, 5, 16);

    internal static Cotacao CriarCotacaoRascunho(
        decimal valorAlvoBrl = 500_000m,
        int prazoMaximoDias = 180)
    {
        return Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(valorAlvoBrl, Moeda.Brl),
            prazoMaximoDias: prazoMaximoDias,
            dataAbertura: DataAberturaPadrao,
            dataPtaxReferencia: new LocalDate(2026, 5, 15),
            ptaxUsadaUsdBrl: PtaxPadrao,
            clock: CriarClock());
    }

    internal static Cotacao CriarCotacaoEmCaptacao(
        decimal valorAlvoBrl = 500_000m,
        int prazoMaximoDias = 180)
    {
        Cotacao cotacao = CriarCotacaoRascunho(valorAlvoBrl, prazoMaximoDias);
        cotacao.Enviar(CriarClock());
        return cotacao;
    }

    internal static Cotacao CriarCotacaoComparada(decimal valorAlvoBrl = 500_000m)
    {
        Cotacao cotacao = CriarCotacaoEmCaptacao(valorAlvoBrl);
        cotacao.EncerrarCaptacao(CriarClock());
        return cotacao;
    }

    internal static LimiteBanco CriarLimiteBanco(
        Guid? bancoId = null,
        decimal valorLimiteBrl = 1_000_000m)
    {
        return LimiteBanco.Criar(
            bancoId: bancoId ?? Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(valorLimiteBrl, Moeda.Brl),
            dataVigenciaInicio: DataAberturaPadrao,
            clock: CriarClock());
    }

    internal static CotacaoFx CriarCotacaoFxUsd(decimal venda = PtaxPadrao)
    {
        return CotacaoFx.Criar(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            new Money(venda - 0.05m, Moeda.Brl),
            new Money(venda, Moeda.Brl),
            "BACEN",
            // Momento deve recair no dia anterior (2026-05-15) em SP (UTC-3) para que
            // dataPtaxReferencia seja estritamente anterior a dataAbertura (2026-05-16).
            // 2026-05-16T09:00Z - 10h = 2026-05-15T23:00Z = 2026-05-15T20:00-03:00.
            AgentInstant.Minus(Duration.FromHours(10)));
    }

    internal static IClock CriarClock()
    {
        IClock clock = NSubstitute.Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    /// <summary>Adiciona proposta USD padrão à cotação (que deve estar em EmCaptacao).</summary>
    internal static Proposta AdicionarPropostaPadrao(
        Cotacao cotacao,
        Guid? bancoId = null,
        decimal taxaAa = 6.5m,
        int prazoDias = 180)
    {
        return cotacao.AdicionarProposta(
            bancoId: bancoId ?? Guid.NewGuid(),
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(100_000m, Moeda.Usd),
            taxaAaPercentual: taxaAa,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.5m,
            prazoDias: prazoDias,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Money(600_000m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataAberturaPadrao);
    }
}
