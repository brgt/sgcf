namespace Sgcf.Domain.Painel;

/// <summary>
/// Tipo de evento que afeta o saldo projetado de um banco num mês.
/// Juros não entram na projeção de saldo (apenas movimento de principal — AD-6).
/// </summary>
public enum TipoEventoProjecao
{
    /// <summary>Reduz o saldo do banco no mês (parcela de principal vencendo).</summary>
    AmortizacaoPrincipal = 1,

    /// <summary>Aumenta o saldo do banco no mês (nova captação contratada ou simulada).</summary>
    Captacao = 2,
}
