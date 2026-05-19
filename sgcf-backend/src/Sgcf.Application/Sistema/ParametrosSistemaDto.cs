namespace Sgcf.Application.Sistema;

/// <summary>
/// DTO de leitura dos parâmetros globais do sistema.
/// </summary>
/// <param name="TetaoMensalCapacidadeBrl">
/// Tetão mensal de movimentação em BRL.
/// <c>null</c> quando não configurado (sem limite).
/// </param>
public sealed record ParametrosSistemaDto(
    decimal? TetaoMensalCapacidadeBrl);
