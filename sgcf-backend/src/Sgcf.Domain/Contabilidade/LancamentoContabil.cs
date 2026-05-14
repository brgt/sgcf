using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contabilidade;

/// <summary>
/// Lançamento contábil diário — representa o acúmulo de juros pro rata de um contrato ativo.
/// Idempotência garantida por UNIQUE constraint em (contrato_id, data, origem).
/// </summary>
public sealed class LancamentoContabil : Entity
{
    public Guid ContratoId { get; private set; }
    public LocalDate Data { get; private set; }

    /// <summary>Identificador da origem do lançamento. Ex.: "PROVISAO_JUROS".</summary>
    public string Origem { get; private set; } = default!;

    internal decimal ValorDecimal { get; private set; }
    public Moeda MoedaContrato { get; private set; }

    /// <summary>Valor do lançamento na moeda original do contrato.</summary>
    public Money Valor => new(ValorDecimal, MoedaContrato);

    public string Descricao { get; private set; } = default!;
    public Instant CriadoEm { get; private set; }

    private LancamentoContabil() { }

    /// <summary>
    /// Cria um lançamento contábil de provisão de juros diária.
    /// </summary>
    /// <param name="contratoId">Identificador do contrato.</param>
    /// <param name="data">Data de competência do lançamento.</param>
    /// <param name="origem">Chave da origem do lançamento (ex: "PROVISAO_JUROS").</param>
    /// <param name="valor">Valor dos juros pro rata do dia na moeda do contrato.</param>
    /// <param name="descricao">Descrição legível do lançamento.</param>
    /// <param name="clock">Relógio injetado — nunca DateTime.Now.</param>
    public static LancamentoContabil Criar(
        Guid contratoId,
        LocalDate data,
        string origem,
        Money valor,
        string descricao,
        IClock clock)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        if (string.IsNullOrWhiteSpace(origem))
        {
            throw new ArgumentException("Origem não pode ser vazia.", nameof(origem));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descricao não pode ser vazia.", nameof(descricao));
        }

        return new LancamentoContabil
        {
            ContratoId = contratoId,
            Data = data,
            Origem = origem,
            ValorDecimal = Math.Round(valor.Valor, 6, MidpointRounding.AwayFromZero),
            MoedaContrato = valor.Moeda,
            Descricao = descricao,
            CriadoEm = clock.GetCurrentInstant()
        };
    }
}
