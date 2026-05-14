using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cronograma;

public static class CronogramaStrategyFactory
{
    public static ICronogramaStrategy Criar(EstruturaAmortizacao estrutura) =>
        estrutura switch
        {
            // TODO: replace stubs as Strategy implementations are merged
            _ => throw new NotSupportedException($"EstruturaAmortizacao '{estrutura}' ainda não implementada.")
        };
}
