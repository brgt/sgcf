using Sgcf.Application.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Infrastructure.Cronograma;

/// <summary>
/// Adaptador de infraestrutura que delega para <see cref="SacStrategy"/> (função pura).
/// Mantém o domínio sem dependência de injeção de dependência.
/// </summary>
internal sealed class SacGeradorStrategy : IGerarSacStrategy
{
    public IReadOnlyList<EventoGeradoSac> GerarSac(EntradaSac entrada) =>
        SacStrategy.Gerar(entrada);
}
