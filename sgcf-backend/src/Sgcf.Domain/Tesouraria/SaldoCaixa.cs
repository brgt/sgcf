using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Tesouraria;

/// <summary>
/// Registra o saldo de caixa de uma conta bancária em uma data de referência específica.
/// Permite upsert: ao atualizar, retorna o valor anterior para fins de auditoria.
/// </summary>
public sealed class SaldoCaixa : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid ContaId { get; private set; }
    public LocalDate DataReferencia { get; private set; }

    // Backing decimal + moeda para persistência — padrão do projeto (ver Contrato.ValorPrincipalDecimal).
    internal decimal ValorDecimal { get; private set; }
    internal Moeda ValorMoeda { get; private set; }

    /// <summary>Saldo monetário na moeda da conta.</summary>
    public Money Valor => new(ValorDecimal, ValorMoeda);

    public string RegistradoPor { get; private set; } = default!;
    public Instant RegistradoEm { get; private set; }

    private SaldoCaixa() { }

    /// <summary>
    /// Cria um novo registro de saldo caixa para a conta e data informadas.
    /// </summary>
    public static SaldoCaixa Criar(
        Guid contaId,
        LocalDate dataReferencia,
        Money valor,
        string registradoPor,
        IClock clock)
    {
        if (contaId == Guid.Empty)
        {
            throw new ArgumentException("ContaId não pode ser vazio.", nameof(contaId));
        }

        if (string.IsNullOrWhiteSpace(registradoPor))
        {
            throw new ArgumentException("RegistradoPor não pode ser vazio.", nameof(registradoPor));
        }

        return new SaldoCaixa
        {
            ContaId = contaId,
            DataReferencia = dataReferencia,
            ValorDecimal = valor.Valor,
            ValorMoeda = valor.Moeda,
            RegistradoPor = registradoPor.Trim(),
            RegistradoEm = clock.GetCurrentInstant()
        };
    }

    /// <summary>
    /// Atualiza o saldo para um novo valor.
    /// </summary>
    /// <returns>
    /// O valor monetário <em>anterior</em> à atualização — necessário para gravar diff de auditoria.
    /// </returns>
    public Money Atualizar(Money novoValor, string novoRegistradoPor, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(novoRegistradoPor))
        {
            throw new ArgumentException("NovoRegistradoPor não pode ser vazio.", nameof(novoRegistradoPor));
        }

        Money valorAntes = Valor;

        ValorDecimal = novoValor.Valor;
        ValorMoeda = novoValor.Moeda;
        RegistradoPor = novoRegistradoPor.Trim();
        RegistradoEm = clock.GetCurrentInstant();

        return valorAntes;
    }

}
