using Sgcf.Application.Alertas;

namespace Sgcf.Application.Alertas.Dtos;

/// <summary>
/// DTO de saída com a contagem de alertas abertos agrupados por severidade.
/// Usado pelo cockpit para exibir os badges de notificação por perfil.
/// </summary>
public sealed record ContadoresAlertaDto(int Critico, int Atencao, int Informativo)
{
    /// <summary>
    /// Mapeia o tipo de domínio <see cref="ContadoresAlerta"/> para o DTO de saída.
    /// </summary>
    public static ContadoresAlertaDto From(ContadoresAlerta contadores) => new(
        Critico:     contadores.Critico,
        Atencao:     contadores.Atencao,
        Informativo: contadores.Informativo);
}
