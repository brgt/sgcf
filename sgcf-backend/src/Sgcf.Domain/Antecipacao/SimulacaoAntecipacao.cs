using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Registro persistido de uma simulação de antecipação.
/// Captura o estado completo da simulação para auditoria e comparação histórica.
/// </summary>
public sealed class SimulacaoAntecipacao : Entity
{
    public Guid ContratoId { get; private set; }
    public TipoAntecipacao TipoAntecipacao { get; private set; }
    public Instant DataSimulacao { get; private set; }
    public LocalDate DataEfetivaProposta { get; private set; }

    internal decimal ValorPrincipalAQuitarValor { get; private set; }
    internal short ValorPrincipalAQuitarMoedaId { get; private set; }
    public Money ValorPrincipalAQuitar =>
        new(ValorPrincipalAQuitarValor, (Moeda)ValorPrincipalAQuitarMoedaId);

    internal decimal ValorTotalSimuladoBrlValor { get; private set; }
    public Money ValorTotalSimuladoBrl => new(ValorTotalSimuladoBrlValor, Moeda.Brl);

    public decimal? CotacaoAplicada { get; private set; }
    public decimal? TaxaMercadoAtualAa { get; private set; }
    public PadraoAntecipacao PadraoAplicado { get; private set; }
    public string ComponentesCustoJson { get; private set; } = default!;

    internal decimal? EconomiaEstimadaBrlValor { get; private set; }
    public Money? EconomiaEstimadaBrl =>
        EconomiaEstimadaBrlValor.HasValue
            ? new Money(EconomiaEstimadaBrlValor.Value, Moeda.Brl)
            : null;

    public string? ObservacoesBanco { get; private set; }
    public string Status { get; private set; } = "SIMULADA";
    public string CreatedBy { get; private set; } = default!;
    public string Source { get; private set; } = default!;
    public Instant CreatedAt { get; private set; }

    private SimulacaoAntecipacao() { }

    /// <summary>
    /// Cria um novo registro de simulação de antecipação.
    /// </summary>
    public static SimulacaoAntecipacao Criar(
        Guid contratoId,
        TipoAntecipacao tipo,
        LocalDate dataEfetivaProposta,
        Money principalAQuitar,
        Money totalSimuladoBrl,
        decimal? cotacaoAplicada,
        decimal? taxaMercadoAtualAa,
        PadraoAntecipacao padrao,
        string componentesCustoJson,
        Money? economiaEstimadaBrl,
        string? observacoesBanco,
        string createdBy,
        string source,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(componentesCustoJson))
        {
            throw new ArgumentException("ComponentesCustoJson não pode ser vazio.", nameof(componentesCustoJson));
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("CreatedBy não pode ser vazio.", nameof(createdBy));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source não pode ser vazio.", nameof(source));
        }

        return new SimulacaoAntecipacao
        {
            ContratoId = contratoId,
            TipoAntecipacao = tipo,
            DataSimulacao = clock.GetCurrentInstant(),
            DataEfetivaProposta = dataEfetivaProposta,
            ValorPrincipalAQuitarValor = principalAQuitar.Valor,
            ValorPrincipalAQuitarMoedaId = (short)principalAQuitar.Moeda,
            ValorTotalSimuladoBrlValor = totalSimuladoBrl.Valor,
            CotacaoAplicada = cotacaoAplicada,
            TaxaMercadoAtualAa = taxaMercadoAtualAa,
            PadraoAplicado = padrao,
            ComponentesCustoJson = componentesCustoJson,
            EconomiaEstimadaBrlValor = economiaEstimadaBrl?.Valor,
            ObservacoesBanco = observacoesBanco,
            CreatedBy = createdBy,
            Source = source,
            CreatedAt = clock.GetCurrentInstant(),
        };
    }
}
