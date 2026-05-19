using NodaTime;
using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Tests.Simulacao;

/// <summary>
/// Fábrica de objetos de domínio para uso nos testes da camada Application.
/// Clock fixado em 2026-05-19T09:00Z para garantir datas futuras válidas (invariante I-2).
/// </summary>
internal static class CenarioSimulacaoTestFactory
{
    internal static readonly Instant AgoraFixa = Instant.FromUtc(2026, 5, 19, 9, 0);

    internal static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgoraFixa);
        return clock;
    }

    /// <summary>Cria um cenário em Rascunho com dados mínimos válidos.</summary>
    internal static CenarioSimulacao CriarCenarioRascunho(IClock clock, string nome = "Realista 2026", int anoBase = 2026)
        => CenarioSimulacao.Criar(nome, anoBase, "usuario-teste", clock);

    /// <summary>Cria um cenário em Ativo.</summary>
    internal static CenarioSimulacao CriarCenarioAtivo(IClock clock)
    {
        CenarioSimulacao cenario = CriarCenarioRascunho(clock);
        cenario.Ativar(clock);
        return cenario;
    }

    /// <summary>Cria um cenário Arquivado.</summary>
    internal static CenarioSimulacao CriarCenarioArquivado(IClock clock)
    {
        CenarioSimulacao cenario = CriarCenarioAtivo(clock);
        cenario.Arquivar(clock);
        return cenario;
    }

    /// <summary>
    /// Cria uma simulação de contratação com dados mínimos válidos para cenário com anoBase 2026.
    /// DataContratacaoPrevista = 2026-07-01 (futuro em relação ao clock fixo 2026-05-19 e dentro do anoBase).
    /// </summary>
    internal static SimulacaoContratacao CriarSimulacao(Guid cenarioId, IClock clock)
        => SimulacaoContratacao.Criar(
            cenarioId,
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 1),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(2m),
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Mensal,
            quantidadeParcelas: 12,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock,
            anoBase: 2026);
}
