using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cronograma;

public static class CronogramaStrategyFactory
{
    public static ICronogramaStrategy Criar(EstruturaAmortizacao estrutura) =>
        estrutura switch
        {
            EstruturaAmortizacao.Bullet => new BulletCronogramaStrategy(),
            EstruturaAmortizacao.Sac => new SacCronogramaStrategy(),
            EstruturaAmortizacao.Price => new PriceCronogramaStrategy(),
            EstruturaAmortizacao.BulletComJurosPeriodicos => new BulletComJurosCronogramaStrategy(),
            _ => throw new NotSupportedException($"EstruturaAmortizacao '{estrutura}' ainda não implementada.")
        };
}
