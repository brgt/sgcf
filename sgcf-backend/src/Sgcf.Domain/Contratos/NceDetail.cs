using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de contrato NCE (Nota de Crédito à Exportação) ou CCE (Cédula de Crédito à Exportação).
/// Tabela de extensão 1:1 com <see cref="Contrato"/> — mesma convenção de FinimpDetail e Lei4131Detail.
/// <para>
/// NCE é isenio de IRRF (crédito de exportação) e sem IOF câmbio (sem conversão cambial).
/// A moeda do contrato NCE deve ser sempre BRL.
/// </para>
/// </summary>
public sealed class NceDetail : Entity
{
    public Guid ContratoId { get; private set; }

    /// <summary>Número do documento NCE emitido pelo banco.</summary>
    public string? NceNumero { get; private set; }

    /// <summary>Data de emissão do documento NCE.</summary>
    public LocalDate? DataEmissao { get; private set; }

    /// <summary>Nome do banco mandatário responsável pela operação NCE.</summary>
    public string? BancoMandatario { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private NceDetail() { }

    /// <summary>
    /// Cria um novo <see cref="NceDetail"/> com os campos opcionais fornecidos.
    /// </summary>
    public static NceDetail Criar(
        Guid contratoId,
        string? nceNumero,
        LocalDate? dataEmissao,
        string? bancoMandatario,
        IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new NceDetail
        {
            ContratoId = contratoId,
            NceNumero = nceNumero,
            DataEmissao = dataEmissao,
            BancoMandatario = bancoMandatario,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
