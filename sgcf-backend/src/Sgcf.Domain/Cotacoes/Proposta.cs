using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Resposta de um banco a uma Cotacao. Criada exclusivamente via Cotacao.RegistrarProposta.
/// Construtor internal impede criação direta fora do agregado raiz.
/// SPEC §3.1, §3.3.
/// </summary>
public sealed class Proposta : Entity
{
    public Guid CotacaoId { get; private set; }
    public Guid BancoId { get; private set; }
    public Moeda MoedaOriginal { get; private set; }

    internal decimal ValorOferecidoMoedaOriginalDecimal { get; private set; }

    /// <summary>Valor principal ofertado na moeda original da proposta.</summary>
    public Money ValorOferecidoMoedaOriginal =>
        new(ValorOferecidoMoedaOriginalDecimal, MoedaOriginal);

    /// <summary>Taxa nominal anualizada, armazenada como fração (0.06 = 6% a.a.).</summary>
    internal decimal TaxaAaPercentualDecimal { get; private set; }

    public decimal TaxaAaPercentual => TaxaAaPercentualDecimal;

    internal decimal IofPercentualDecimal { get; private set; }
    public decimal IofPercentual => IofPercentualDecimal;

    internal decimal SpreadAaPercentualDecimal { get; private set; }
    public decimal SpreadAaPercentual => SpreadAaPercentualDecimal;

    public int PrazoDias { get; private set; }
    public EstruturaAmortizacao EstruturaAmortizacao { get; private set; }
    public Periodicidade PeriodicidadeJuros { get; private set; }

    public bool ExigeNdf { get; private set; }

    internal decimal? CustoNdfAaPercentualDecimal { get; private set; }
    public decimal? CustoNdfAaPercentual => CustoNdfAaPercentualDecimal;

    public string GarantiaExigida { get; private set; } = string.Empty;

    internal decimal ValorGarantiaExigidaBrlDecimal { get; private set; }
    public Money ValorGarantiaExigidaBrl =>
        new(ValorGarantiaExigidaBrlDecimal, Moeda.Brl);

    public bool GarantiaEhCdbCativo { get; private set; }

    internal decimal? RendimentoCdbAaPercentualDecimal { get; private set; }
    public decimal? RendimentoCdbAaPercentual => RendimentoCdbAaPercentualDecimal;

    /// <summary>
    /// CET calculado em % a.a., armazenado como cache.
    /// Invalidado toda vez que qualquer dado da proposta muda.
    /// null indica que precisa ser (re)calculado.
    /// </summary>
    public decimal? CetCalculadoAaPercentual { get; private set; }

    internal decimal? ValorTotalEstimadoBrlDecimal { get; private set; }

    /// <summary>Custo total estimado em BRL, calculado como cache junto com o CET.</summary>
    public Money? ValorTotalEstimadoBrl =>
        ValorTotalEstimadoBrlDecimal.HasValue
            ? new Money(ValorTotalEstimadoBrlDecimal.Value, Moeda.Brl)
            : null;

    public LocalDate DataCaptura { get; private set; }
    public LocalDate? DataValidadeMercado { get; private set; }

    public StatusProposta Status { get; private set; }
    public string? MotivoRecusa { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private Proposta() { }

    /// <summary>
    /// Construtor internal: criação exclusiva via Cotacao.RegistrarProposta.
    /// Valida invariantes SPEC §3.3.
    /// </summary>
    internal Proposta(
        Guid cotacaoId,
        Guid bancoId,
        Moeda moedaOriginal,
        Money valorOferecidoMoedaOriginal,
        decimal taxaAaPercentual,
        decimal iofPercentual,
        decimal spreadAaPercentual,
        int prazoDias,
        EstruturaAmortizacao estruturaAmortizacao,
        Periodicidade periodicidadeJuros,
        bool exigeNdf,
        decimal? custoNdfAaPercentual,
        string garantiaExigida,
        Money valorGarantiaExigidaBrl,
        bool garantiaEhCdbCativo,
        decimal? rendimentoCdbAaPercentual,
        LocalDate dataCaptura,
        LocalDate? dataValidadeMercado = null)
    {
        ValidarInvariantes(
            taxaAaPercentual,
            prazoDias,
            exigeNdf,
            custoNdfAaPercentual,
            garantiaEhCdbCativo,
            rendimentoCdbAaPercentual);

        CotacaoId = cotacaoId;
        BancoId = bancoId;
        MoedaOriginal = moedaOriginal;
        ValorOferecidoMoedaOriginalDecimal = valorOferecidoMoedaOriginal.Valor;
        TaxaAaPercentualDecimal = taxaAaPercentual;
        IofPercentualDecimal = iofPercentual;
        SpreadAaPercentualDecimal = spreadAaPercentual;
        PrazoDias = prazoDias;
        EstruturaAmortizacao = estruturaAmortizacao;
        PeriodicidadeJuros = periodicidadeJuros;
        ExigeNdf = exigeNdf;
        CustoNdfAaPercentualDecimal = custoNdfAaPercentual;
        GarantiaExigida = garantiaExigida ?? string.Empty;
        ValorGarantiaExigidaBrlDecimal = valorGarantiaExigidaBrl.Valor;
        GarantiaEhCdbCativo = garantiaEhCdbCativo;
        RendimentoCdbAaPercentualDecimal = rendimentoCdbAaPercentual;
        DataCaptura = dataCaptura;
        DataValidadeMercado = dataValidadeMercado;
        Status = StatusProposta.Recebida;

        // Cache inválido: será calculado na primeira chamada
        CetCalculadoAaPercentual = null;
        ValorTotalEstimadoBrlDecimal = null;
    }

    /// <summary>
    /// Atualiza o cache de CET e valor total estimado.
    /// Chamado internamente após cálculo via CalculadoraCet.
    /// </summary>
    internal void AtualizarCacheCalculos(decimal cetAaPercentual, Money valorTotalEstimadoBrl)
    {
        CetCalculadoAaPercentual = cetAaPercentual;
        ValorTotalEstimadoBrlDecimal = valorTotalEstimadoBrl.Valor;
    }

    /// <summary>
    /// Invalida o cache de cálculos quando dados da proposta mudam.
    /// Força recalculação na próxima invocação de CalculadoraCet.
    /// </summary>
    internal void InvalidarCacheCalculos()
    {
        CetCalculadoAaPercentual = null;
        ValorTotalEstimadoBrlDecimal = null;
    }

    /// <summary>Marca a proposta como aceita pela Cotacao pai.</summary>
    internal void Aceitar()
    {
        Status = StatusProposta.Aceita;
    }

    /// <summary>Reverte aceitação para Recebida quando Cotacao desfaz aceitação.</summary>
    internal void ReverterAceitacao()
    {
        Status = StatusProposta.Recebida;
    }

    /// <summary>Marca proposta como recusada com motivo opcional.</summary>
    internal void Recusar(string? motivo = null)
    {
        Status = StatusProposta.Recusada;
        MotivoRecusa = motivo;
    }

    /// <summary>Marca proposta como expirada (snapshot de mercado desatualizado).</summary>
    internal void Expirar()
    {
        Status = StatusProposta.Expirada;
    }

    private static void ValidarInvariantes(
        decimal taxaAaPercentual,
        int prazoDias,
        bool exigeNdf,
        decimal? custoNdfAaPercentual,
        bool garantiaEhCdbCativo,
        decimal? rendimentoCdbAaPercentual)
    {
        if (taxaAaPercentual < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taxaAaPercentual),
                "TaxaAaPercentual não pode ser negativa (SPEC §3.3).");
        }

        if (prazoDias < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prazoDias),
                "PrazoDias deve ser maior ou igual a 1 (SPEC §3.3).");
        }

        if (exigeNdf && custoNdfAaPercentual is null)
        {
            throw new ArgumentException(
                "CustoNdfAaPercentual é obrigatório quando ExigeNdf = true (SPEC §3.3).",
                nameof(custoNdfAaPercentual));
        }

        if (garantiaEhCdbCativo && rendimentoCdbAaPercentual is null)
        {
            throw new ArgumentException(
                "RendimentoCdbAaPercentual é obrigatório quando GarantiaEhCdbCativo = true (SPEC §3.3).",
                nameof(rendimentoCdbAaPercentual));
        }
    }
}
