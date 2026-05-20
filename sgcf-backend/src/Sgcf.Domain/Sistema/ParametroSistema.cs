using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Sistema;

/// <summary>
/// Parâmetros globais do sistema — singleton (uma única linha na tabela).
/// Armazena configurações de controle operacional aplicadas a todo o portfólio.
///
/// Decisão D-11 (Task 3.4): tetão mensal configurável via esta entidade.
/// O campo <see cref="TetaoMensalCapacidadeBrl"/> limita a soma de captações +
/// amortizações por mês. Quando excedido, gera alertas no QuadroDivida (não bloqueia).
///
/// Design singleton: a chave <see cref="Chave"/> é sempre <c>"GLOBAL"</c>.
/// Não existe multi-tenant neste MVP — uma única instância serve todo o sistema.
/// </summary>
public sealed class ParametroSistema : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>Chave fixa que garante o singleton — sempre "GLOBAL".</summary>
    public const string ChaveGlobal = "GLOBAL";

    /// <summary>Discriminador de linha — valor sempre igual a <see cref="ChaveGlobal"/>.</summary>
    public string Chave { get; private set; } = ChaveGlobal;

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
    /// Cria o singleton global de parâmetros.
    /// Sem tetão configurado — deve ser chamado apenas na seed da migration.
    /// </summary>
    public static ParametroSistema Criar(IClock clock) =>
        new()
        {
            Chave = ChaveGlobal,
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
