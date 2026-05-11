using Sgcf.Application.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Infrastructure.Cronograma;

internal sealed class BulletGeradorStrategy : IGeradorCronograma
{
    public IReadOnlyList<EventoGeradoBullet> Gerar(EntradaBullet entrada) =>
        BulletStrategy.Gerar(entrada);
}
