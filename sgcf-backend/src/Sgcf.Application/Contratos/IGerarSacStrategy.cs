using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos;

/// <summary>
/// Contrato da estratégia de geração de cronograma SAC (Sistema de Amortização Constante).
/// Implementado em Infrastructure por <c>SacGeradorStrategy</c>, que delega para o
/// pure-function <c>SacStrategy.Gerar</c>.
/// </summary>
public interface IGerarSacStrategy
{
    public IReadOnlyList<EventoGeradoSac> GerarSac(EntradaSac entrada);
}
