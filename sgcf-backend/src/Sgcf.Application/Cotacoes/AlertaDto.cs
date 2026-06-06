namespace Sgcf.Application.Cotacoes;

/// <summary>Severidade de um alerta de validação suave. Nunca bloqueante. SPEC S40 §4.6.</summary>
public enum SeveridadeAlertaCotacao
{
    Info,
    Aviso,
}

/// <summary>
/// Alerta de validação suave retornado nas respostas de escrita de cotação. Não bloqueia a operação;
/// o front-end ramifica por <see cref="Codigo"/> (estável) e exibe <see cref="Mensagem"/>. SPEC S40 §4.6.
/// </summary>
/// <param name="Codigo">Código estável, legível por máquina (ex.: "prazo-recalculado").</param>
/// <param name="Campo">Campo de origem do alerta (para realce inline no FE).</param>
/// <param name="Severidade">Info ou Aviso.</param>
/// <param name="Mensagem">Texto legível para exibição ao operador.</param>
public sealed record AlertaDto(
    string Codigo,
    string Campo,
    SeveridadeAlertaCotacao Severidade,
    string Mensagem);
