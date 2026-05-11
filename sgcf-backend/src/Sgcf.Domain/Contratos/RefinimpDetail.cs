using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de contrato REFINIMP — refinanciamento de importação.
/// Tabela de extensão 1:1 com <see cref="Contrato"/> (mesma convenção de FinimpDetail e Lei4131Detail).
/// <para>
/// <c>ContratoMaeId</c> referencia o contrato diretamente refinanciado (pode ele próprio ser um REFINIMP).
/// Para a regra de 70% BB, sempre use o ancestral não-REFINIMP obtido via
/// <c>IContratoRepository.GetAncestraNaoRefinimpAsync</c>.
/// </para>
/// </summary>
public sealed class RefinimpDetail : Entity
{
    /// <summary>Id do contrato REFINIMP ao qual este detalhe pertence.</summary>
    public Guid ContratoId { get; private set; }

    /// <summary>
    /// Id do contrato diretamente refinanciado (contrato mãe imediata).
    /// Pode ser ele próprio um REFINIMP em cadeias recursivas.
    /// </summary>
    public Guid ContratoMaeId { get; private set; }

    /// <summary>
    /// Percentual do principal do contrato mãe que este REFINIMP cobre.
    /// Armazenado como fração (0..1). Ex: 0.70 para 70%.
    /// </summary>
    internal decimal PercentualRefinanciadoDecimal { get; private set; }

    public Percentual PercentualRefinanciado => Percentual.DeFracao(PercentualRefinanciadoDecimal);

    /// <summary>
    /// Valor efetivamente quitado no contrato mãe por este refinanciamento.
    /// Corresponde a <c>ValorPrincipal</c> do contrato REFINIMP.
    /// </summary>
    internal decimal ValorQuitadoNoRefiValor { get; private set; }
    internal Moeda ValorQuitadoNoRefiMoeda { get; private set; }

    public Money ValorQuitadoNoRefi => new(ValorQuitadoNoRefiValor, ValorQuitadoNoRefiMoeda);

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private RefinimpDetail() { }

    /// <summary>
    /// Cria um novo <see cref="RefinimpDetail"/>.
    /// </summary>
    /// <param name="contratoId">Id do contrato REFINIMP recém-criado.</param>
    /// <param name="contratoMaeId">Id do contrato diretamente refinanciado.</param>
    /// <param name="percentualRefinanciado">
    /// Percentual do principal ancestral que este REFINIMP cobre; fração (0..1).
    /// </param>
    /// <param name="valorQuitadoNoRefi">
    /// Valor que está sendo efetivamente refinanciado (= ValorPrincipal do REFINIMP).
    /// </param>
    /// <param name="clock">Relógio injetado para auditoria.</param>
    public static RefinimpDetail Criar(
        Guid contratoId,
        Guid contratoMaeId,
        Percentual percentualRefinanciado,
        Money valorQuitadoNoRefi,
        IClock clock)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        if (contratoMaeId == Guid.Empty)
        {
            throw new ArgumentException("ContratoMaeId não pode ser vazio.", nameof(contratoMaeId));
        }

        Instant now = clock.GetCurrentInstant();
        return new RefinimpDetail
        {
            ContratoId = contratoId,
            ContratoMaeId = contratoMaeId,
            PercentualRefinanciadoDecimal = percentualRefinanciado.AsDecimal,
            ValorQuitadoNoRefiValor = valorQuitadoNoRefi.Valor,
            ValorQuitadoNoRefiMoeda = valorQuitadoNoRefi.Moeda,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
