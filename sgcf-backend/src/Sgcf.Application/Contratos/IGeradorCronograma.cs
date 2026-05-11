using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos;

public interface IGeradorCronograma
{
    public IReadOnlyList<EventoGeradoBullet> Gerar(EntradaBullet entrada);
}
