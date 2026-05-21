namespace Sgcf.Application.Preferencias;

/// <summary>
/// Representação de uma preferência de usuário para transporte via API.
/// </summary>
/// <param name="Chave">Chave da preferência (ex: "cockpit.layout", "theme").</param>
/// <param name="Valor">Valor serializado da preferência.</param>
/// <param name="AtualizadoEm">Instante ISO-8601 da última atualização.</param>
public sealed record PreferenciaUsuarioDto(string Chave, string Valor, string AtualizadoEm);
