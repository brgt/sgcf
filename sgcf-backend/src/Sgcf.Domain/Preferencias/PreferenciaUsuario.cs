using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Preferencias;

/// <summary>
/// Preferência de UI persistida por usuário e tenant.
///
/// Cada registro armazena um par (chave → valor) para um usuário específico dentro do tenant.
/// O valor é sempre uma string — o front-end serializa objetos complexos em JSON antes de enviar.
///
/// Restrição única: (TenantId, UserId, Chave) — um valor por usuário por chave.
/// TenantId é preenchido automaticamente pelo <c>TenantSaveInterceptor</c>.
/// </summary>
public sealed class PreferenciaUsuario : Entity, ITenantScoped
{
    /// <inheritdoc />
    /// <remarks>Preenchido automaticamente pelo TenantSaveInterceptor — nunca setar manualmente.</remarks>
    public Guid TenantId { get; private set; }

    /// <summary>O claim <c>sub</c> do JWT do usuário (ID do identity provider externo).</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Chave da preferência. Exemplos: <c>"cockpit.layout"</c>, <c>"theme"</c>, <c>"filtros.padrao"</c>.
    /// Máximo de 100 caracteres.
    /// </summary>
    public string Chave { get; private set; } = string.Empty;

    /// <summary>
    /// Valor da preferência como string.
    /// O front-end serializa objetos complexos para JSON antes de enviar.
    /// Máximo de 4000 caracteres.
    /// </summary>
    public string Valor { get; private set; } = string.Empty;

    /// <summary>Instante UTC da última atualização desta preferência.</summary>
    public Instant AtualizadoEm { get; private set; }

    /// <summary>EF Core requer construtor sem parâmetros para materialização.</summary>
    private PreferenciaUsuario() { }

    /// <summary>
    /// Cria uma nova preferência de usuário com os valores informados.
    /// </summary>
    /// <param name="userId">ID do usuário (claim <c>sub</c> do JWT). Não pode ser nulo ou vazio.</param>
    /// <param name="chave">Chave da preferência. Não pode ser nula/vazia; máximo 100 caracteres.</param>
    /// <param name="valor">Valor da preferência. Não pode ser nulo; máximo 4000 caracteres.</param>
    /// <param name="agora">Instante de criação, obtido via <c>IClock</c>.</param>
    /// <returns>Nova instância de <see cref="PreferenciaUsuario"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Quando <paramref name="userId"/> ou <paramref name="chave"/> são nulos/vazios,
    /// ou quando <paramref name="chave"/> ultrapassa 100 caracteres,
    /// ou quando <paramref name="valor"/> ultrapassa 4000 caracteres.
    /// </exception>
    public static PreferenciaUsuario Criar(string userId, string chave, string valor, Instant agora)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId não pode ser nulo ou vazio.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new ArgumentException("Chave não pode ser nula ou vazia.", nameof(chave));
        }

        if (chave.Length > 100)
        {
            throw new ArgumentException($"Chave não pode exceder 100 caracteres. Comprimento: {chave.Length}.", nameof(chave));
        }

        ArgumentNullException.ThrowIfNull(valor, nameof(valor));

        if (valor.Length > 4000)
        {
            throw new ArgumentException($"Valor não pode exceder 4000 caracteres. Comprimento: {valor.Length}.", nameof(valor));
        }

        return new PreferenciaUsuario
        {
            UserId = userId,
            Chave = chave,
            Valor = valor,
            AtualizadoEm = agora
        };
    }

    /// <summary>
    /// Atualiza o valor desta preferência e registra o instante de modificação.
    /// </summary>
    /// <param name="novoValor">Novo valor. Não pode ser nulo; máximo 4000 caracteres.</param>
    /// <param name="agora">Instante da atualização, obtido via <c>IClock</c>.</param>
    /// <exception cref="ArgumentException">Quando <paramref name="novoValor"/> ultrapassa 4000 caracteres.</exception>
    public void AtualizarValor(string novoValor, Instant agora)
    {
        ArgumentNullException.ThrowIfNull(novoValor, nameof(novoValor));

        if (novoValor.Length > 4000)
        {
            throw new ArgumentException($"Valor não pode exceder 4000 caracteres. Comprimento: {novoValor.Length}.", nameof(novoValor));
        }

        Valor = novoValor;
        AtualizadoEm = agora;
    }
}
