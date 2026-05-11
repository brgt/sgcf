using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Entidade mestre de garantia vinculada a um <see cref="Contrato"/>.
/// O valor é sempre em BRL para permitir agregação; cada tipo de garantia
/// possui uma entidade de detalhe separada (tabela de extensão 1:1).
/// </summary>
public sealed class Garantia : Entity
{
    public Guid ContratoId { get; private set; }
    public TipoGarantia Tipo { get; private set; }

    /// <summary>Valor BRL armazenado como decimal — use <see cref="ValorBrl"/> para acesso tipado.</summary>
    internal decimal ValorBrlDecimal { get; private set; }

    /// <summary>Valor da garantia em BRL (sempre BRL para permitir consolidação).</summary>
    public Money ValorBrl => new(ValorBrlDecimal, Moeda.Brl);

    /// <summary>
    /// Percentual do principal do contrato coberto por esta garantia, armazenado como fração (0..1).
    /// Null quando o principal do contrato é zero ou não fornecido.
    /// </summary>
    internal decimal? PercentualPrincipalDecimal { get; private set; }

    /// <summary>Percentual do principal coberto por esta garantia. Null quando indisponível.</summary>
    public Percentual? PercentualPrincipal =>
        PercentualPrincipalDecimal.HasValue
            ? Percentual.DeFracao(PercentualPrincipalDecimal.Value)
            : null;

    public LocalDate DataConstituicao { get; private set; }
    public LocalDate? DataLiberacaoPrevista { get; private set; }
    public LocalDate? DataLiberacaoEfetiva { get; private set; }
    public StatusGarantia Status { get; private set; }
    public string? Observacoes { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private Garantia() { }

    /// <summary>
    /// Cria uma nova garantia.
    /// </summary>
    /// <param name="contratoId">Id do contrato pai.</param>
    /// <param name="tipo">Tipo da garantia (determina qual entidade de detalhe deve ser criada).</param>
    /// <param name="valorBrl">Valor da garantia em BRL.</param>
    /// <param name="principalBrlParaCalculo">
    /// Valor do principal do contrato em BRL, usado para calcular <see cref="PercentualPrincipal"/>.
    /// Null ou zero resulta em <see cref="PercentualPrincipal"/> nulo.
    /// </param>
    /// <param name="dataConstituicao">Data de constituição da garantia.</param>
    /// <param name="dataLiberacaoPrevista">Data prevista de liberação. Deve ser posterior à constituição.</param>
    /// <param name="observacoes">Observações opcionais.</param>
    /// <param name="createdBy">Identificador do usuário que criou o registro.</param>
    /// <param name="clock">Relógio injetado para timestamp determinístico.</param>
    public static Garantia Criar(
        Guid contratoId,
        TipoGarantia tipo,
        Money valorBrl,
        decimal? principalBrlParaCalculo,
        LocalDate dataConstituicao,
        LocalDate? dataLiberacaoPrevista,
        string? observacoes,
        string createdBy,
        IClock clock)
    {
        if (valorBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("O valor da garantia deve ser informado em BRL.", nameof(valorBrl));
        }

        if (dataLiberacaoPrevista.HasValue && dataLiberacaoPrevista.Value <= dataConstituicao)
        {
            throw new ArgumentException(
                "DataLiberacaoPrevista deve ser posterior a DataConstituicao.", nameof(dataLiberacaoPrevista));
        }

        decimal? percentualFracao = null;
        if (principalBrlParaCalculo.HasValue && principalBrlParaCalculo.Value != 0m)
        {
            percentualFracao = Math.Round(
                valorBrl.Valor / principalBrlParaCalculo.Value,
                decimals: 6,
                MidpointRounding.AwayFromZero);
        }

        Instant now = clock.GetCurrentInstant();
        return new Garantia
        {
            ContratoId = contratoId,
            Tipo = tipo,
            ValorBrlDecimal = valorBrl.Valor,
            PercentualPrincipalDecimal = percentualFracao,
            DataConstituicao = dataConstituicao,
            DataLiberacaoPrevista = dataLiberacaoPrevista,
            DataLiberacaoEfetiva = null,
            Status = StatusGarantia.Ativa,
            Observacoes = observacoes,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Libera a garantia, registrando a data efetiva de liberação.</summary>
    public void Liberar(LocalDate dataEfetiva, IClock clock)
    {
        Status = StatusGarantia.Liberada;
        DataLiberacaoEfetiva = dataEfetiva;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>Marca a garantia como executada pelo credor.</summary>
    public void Executar(IClock clock)
    {
        Status = StatusGarantia.Executada;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>Cancela a garantia.</summary>
    public void Cancelar(IClock clock)
    {
        Status = StatusGarantia.Cancelada;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
