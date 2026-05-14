namespace Sgcf.Domain.Cronograma;

public interface ICronogramaStrategy
{
    public IReadOnlyList<EventoCronogramaGerado> Gerar(GerarCronogramaInput input);
}
