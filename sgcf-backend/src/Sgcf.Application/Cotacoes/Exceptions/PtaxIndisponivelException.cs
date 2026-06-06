namespace Sgcf.Application.Cotacoes.Exceptions;

/// <summary>
/// PTAX D-1 indisponível para a moeda e a data de referência informadas.
/// Mapeada para HTTP 409 ProblemDetails com type estável e extensões
/// <c>moedaAlvo</c>/<c>dataPtaxReferencia</c>. SPEC S40 §5, §6.
/// Especialização de <see cref="InvalidOperationException"/> para mapeamento tipado central.
/// </summary>
public sealed class PtaxIndisponivelException : InvalidOperationException
{
    public PtaxIndisponivelException(string moedaAlvo, DateOnly? dataReferencia, string message)
        : base(message)
    {
        MoedaAlvo = moedaAlvo;
        DataReferencia = dataReferencia;
    }

    /// <summary>Moeda alvo cuja PTAX não foi encontrada (ex.: "Usd", "Eur").</summary>
    public string MoedaAlvo { get; }

    /// <summary>Data de referência pretendida (D-1), quando conhecida.</summary>
    public DateOnly? DataReferencia { get; }
}
