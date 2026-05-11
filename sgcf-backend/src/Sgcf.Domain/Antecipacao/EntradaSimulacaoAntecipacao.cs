using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Dados de entrada para simular qualquer padrão de antecipação.
/// Imutável — criada na camada de aplicação, consumida pelas estratégias puras.
/// </summary>
public sealed record EntradaSimulacaoAntecipacao(
    TipoAntecipacao Tipo,
    Money PrincipalAQuitar,
    Money? JurosProRata,
    int PrazoTotalOriginalDias,
    int PrazoRemanescenteDias,
    int PrazoRemanescenteMeses,
    Percentual TaxaAa,
    int BaseCalculo,
    Percentual? TaxaMercadoAtualAa,
    Money? IndenizacaoBanco,
    bool OrigemRefinanciamentoInterno
);
