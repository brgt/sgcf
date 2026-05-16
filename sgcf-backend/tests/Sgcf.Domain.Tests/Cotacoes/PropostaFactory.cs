using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Helper de fábrica para criar objetos de teste.
/// Evita duplicação de arrange em múltiplos testes.
/// Usa NSubstitute para mock de IClock (padrão do projeto).
/// </summary>
internal static class PropostaFactory
{
    internal static IClock CriarClockFixo(int ano = 2026, int mes = 5, int dia = 15)
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(ano, mes, dia, 12, 0, 0));
        return clock;
    }

    internal static Proposta CriarProposta(
        Guid? cotacaoId = null,
        Guid? bancoId = null,
        Moeda moedaOriginal = Moeda.Usd,
        decimal valorOferecido = 1_000_000m,
        decimal taxaAaPercentual = 5m,
        decimal iofPercentual = 0.38m,
        decimal spreadAaPercentual = 0.5m,
        int prazoDias = 180,
        EstruturaAmortizacao estrutura = EstruturaAmortizacao.Bullet,
        Periodicidade periodicidade = Periodicidade.Bullet,
        bool exigeNdf = false,
        decimal? custoNdfAaPercentual = null,
        string garantiaExigida = "Aval",
        decimal valorGarantia = 500_000m,
        bool garantiaEhCdbCativo = false,
        decimal? rendimentoCdbAaPercentual = null,
        LocalDate? dataCaptura = null)
    {
        return new Proposta(
            cotacaoId: cotacaoId ?? Guid.NewGuid(),
            bancoId: bancoId ?? Guid.NewGuid(),
            moedaOriginal: moedaOriginal,
            valorOferecidoMoedaOriginal: new Money(valorOferecido, moedaOriginal),
            taxaAaPercentual: taxaAaPercentual,
            iofPercentual: iofPercentual,
            spreadAaPercentual: spreadAaPercentual,
            prazoDias: prazoDias,
            estruturaAmortizacao: estrutura,
            periodicidadeJuros: periodicidade,
            exigeNdf: exigeNdf,
            custoNdfAaPercentual: custoNdfAaPercentual,
            garantiaExigida: garantiaExigida,
            valorGarantiaExigidaBrl: new Money(valorGarantia, Moeda.Brl),
            garantiaEhCdbCativo: garantiaEhCdbCativo,
            rendimentoCdbAaPercentual: rendimentoCdbAaPercentual,
            dataCaptura: dataCaptura ?? new LocalDate(2026, 5, 15));
    }

    internal static Cotacao CriarCotacaoRascunho(
        string codigoInterno = "COT-2026-00001",
        decimal valorAlvo = 5_000_000m,
        int prazoMaximoDias = 180,
        decimal ptax = 5.20m)
    {
        var clock = CriarClockFixo();
        return Cotacao.Criar(
            codigoInterno: codigoInterno,
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(valorAlvo, Moeda.Brl),
            prazoMaximoDias: prazoMaximoDias,
            dataAbertura: new LocalDate(2026, 5, 15),
            dataPtaxReferencia: new LocalDate(2026, 5, 14),
            ptaxUsadaUsdBrl: ptax,
            clock: clock);
    }
}
