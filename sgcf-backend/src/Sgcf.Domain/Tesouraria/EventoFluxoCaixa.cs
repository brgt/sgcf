using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Tesouraria;

/// <summary>
/// Registra um evento manual de fluxo de caixa (entrada ou saída) em uma data específica.
/// Complementa o fluxo projetado gerado automaticamente a partir do cronograma de contratos.
/// </summary>
public sealed class EventoFluxoCaixa : Entity, ITenantScoped
{
    /// <summary>Tenant dono do registro — preenchido automaticamente pelo <c>TenantSaveInterceptor</c>.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Data de competência do evento (dia em que o fluxo ocorre ou está previsto).</summary>
    public LocalDate Data { get; private set; }

    /// <summary>Direção financeira: Entrada ou Saída.</summary>
    public TipoEventoFluxo Tipo { get; private set; }

    // Backing fields para persistência — padrão do projeto (ver SaldoCaixa.ValorDecimal).
    internal decimal ValorDecimal { get; private set; }
    internal Moeda ValorMoeda { get; private set; }

    /// <summary>Valor monetário do evento na moeda informada.</summary>
    public Money Valor => new(ValorDecimal, ValorMoeda);

    /// <summary>Descrição livre do evento. Máximo 500 caracteres.</summary>
    public string Descricao { get; private set; } = default!;

    /// <summary>Identificador do usuário que registrou o evento (ex.: email ou sub JWT).</summary>
    public string RegistradoPor { get; private set; } = default!;

    /// <summary>Instante UTC em que o evento foi registrado no sistema.</summary>
    public Instant RegistradoEm { get; private set; }

    private EventoFluxoCaixa() { }

    /// <summary>
    /// Cria um novo evento de fluxo de caixa validando as regras de negócio básicas.
    /// </summary>
    /// <param name="data">Data de competência.</param>
    /// <param name="tipo">Entrada ou Saída.</param>
    /// <param name="valor">Valor monetário — deve ser positivo.</param>
    /// <param name="descricao">Descrição livre — máximo 500 caracteres.</param>
    /// <param name="registradoPor">Identificador do usuário.</param>
    /// <param name="clock">Fonte de tempo injetada — nunca <c>DateTime.Now</c>.</param>
    public static EventoFluxoCaixa Criar(
        LocalDate data,
        TipoEventoFluxo tipo,
        Money valor,
        string descricao,
        string registradoPor,
        IClock clock)
    {
        if (valor.Valor <= 0m)
        {
            throw new ArgumentException("O valor do evento de fluxo de caixa deve ser positivo.", nameof(valor));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descrição não pode ser vazia.", nameof(descricao));
        }

        if (descricao.Length > 500)
        {
            throw new ArgumentException("Descrição não pode exceder 500 caracteres.", nameof(descricao));
        }

        if (string.IsNullOrWhiteSpace(registradoPor))
        {
            throw new ArgumentException("RegistradoPor não pode ser vazio.", nameof(registradoPor));
        }

        return new EventoFluxoCaixa
        {
            Data = data,
            Tipo = tipo,
            ValorDecimal = valor.Valor,
            ValorMoeda = valor.Moeda,
            Descricao = descricao.Trim(),
            RegistradoPor = registradoPor.Trim(),
            RegistradoEm = clock.GetCurrentInstant()
        };
    }
}
