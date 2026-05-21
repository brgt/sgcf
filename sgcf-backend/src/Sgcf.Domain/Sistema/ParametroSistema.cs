using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Sistema;

/// <summary>
/// Parâmetros de sistema por tenant.
/// Armazena configurações de controle operacional do portfólio do tenant.
///
/// Decisão D-11 (Task 3.4): tetão mensal configurável via esta entidade.
/// O campo <see cref="TetaoMensalCapacidadeBrl"/> limita a soma de captações +
/// amortizações por mês. Quando excedido, gera alertas no QuadroDivida (não bloqueia).
///
/// Task −1.9: refatorado de singleton global para per-tenant.
/// Cada tenant tem exatamente uma linha com <see cref="Chave"/> = <c>"DEFAULT"</c>.
/// O discriminador <see cref="Chave"/> existe para extensão futura (múltiplos
/// conjuntos de parâmetros por tenant) sem alteração de schema.
/// </summary>
public sealed class ParametroSistema : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>Discriminador de linha — valor padrão <c>"DEFAULT"</c>.</summary>
    public string Chave { get; private set; } = "DEFAULT";

    /// <summary>
    /// Valor decimal persistido internamente. Nullable — null indica "sem tetão configurado".
    /// Exposto como <c>internal</c> para que o EF possa mapear sem propriedade navigation pública.
    /// </summary>
    internal decimal? TetaoMensalCapacidadeBrlDecimal { get; private set; }

    /// <summary>
    /// Tetão mensal de movimentação em BRL.
    /// Quando não nulo e algum mês tiver (captações + amortizações) > tetão,
    /// o <see cref="Sgcf.Application.Painel.ValidadorTetaoMensal"/> emite um alerta.
    /// Nulo significa "sem limite configurado".
    /// </summary>
    public Money? TetaoMensalCapacidadeBrl =>
        TetaoMensalCapacidadeBrlDecimal.HasValue
            ? new Money(TetaoMensalCapacidadeBrlDecimal.Value, Moeda.Brl)
            : null;

    /// <summary>Timestamp da última atualização desta configuração.</summary>
    public Instant UpdatedAt { get; private set; }

    /// <summary>EF Core requer construtor sem parâmetros para materialização.</summary>
    private ParametroSistema() { }

    /// <summary>
    /// Cria os parâmetros de sistema padrão para um tenant específico.
    ///
    /// <para>
    /// TenantId deve ser informado explicitamente porque este método é chamado
    /// pelo provisionador — que opera fora do contexto de request do tenant alvo
    /// (sem <c>TenantSaveInterceptor</c> ativo).
    /// </para>
    /// </summary>
    /// <param name="tenantId">Identificador do tenant dono destes parâmetros.</param>
    /// <param name="clock">Relógio para timestamp inicial.</param>
    public static ParametroSistema CriarDefault(Guid tenantId, IClock clock) =>
        new()
        {
            TenantId = tenantId,
            Chave = "DEFAULT",
            TetaoMensalCapacidadeBrlDecimal = null,
            UpdatedAt = clock.GetCurrentInstant()
        };

    /// <summary>
    /// Atualiza o tetão mensal de movimentação.
    /// </summary>
    /// <param name="valor">
    /// Novo valor em BRL. Passe <c>null</c> para remover o limite.
    /// </param>
    /// <param name="clock">Relógio para timestamp de auditoria.</param>
    /// <exception cref="ArgumentException">
    /// Quando <paramref name="valor"/> não está em BRL.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Quando <paramref name="valor"/> é negativo.
    /// </exception>
    public void AtualizarTetaoMensal(Money? valor, IClock clock)
    {
        if (valor.HasValue && valor.Value.Moeda != Moeda.Brl)
        {
            throw new ArgumentException(
                "Tetão deve ser em BRL. Recebido: " + valor.Value.Moeda,
                nameof(valor));
        }

        if (valor.HasValue && valor.Value.Valor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(valor),
                "Tetão não pode ser negativo.");
        }

        TetaoMensalCapacidadeBrlDecimal = valor?.Valor;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
