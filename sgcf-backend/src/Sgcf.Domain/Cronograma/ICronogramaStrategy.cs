namespace Sgcf.Domain.Cronograma;

public interface ICronogramaStrategy
{
    public IReadOnlyList<EventoCronograma> Gerar(GerarCronogramaInput input);
}
