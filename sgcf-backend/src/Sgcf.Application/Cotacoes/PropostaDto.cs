using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public sealed record PropostaDto(
    Guid Id,
    Guid CotacaoId,
    Guid BancoId,
    string MoedaOriginal,
    decimal ValorOferecidoMoedaOriginal,
    decimal TaxaAaPercentual,
    decimal IofPercentual,
    decimal SpreadAaPercentual,
    int PrazoDias,
    string EstruturaAmortizacao,
    string PeriodicidadeJuros,
    bool ExigeNdf,
    decimal? CustoNdfAaPercentual,
    string GarantiaExigida,
    decimal ValorGarantiaExigidaBrl,
    bool GarantiaEhCdbCativo,
    decimal? RendimentoCdbAaPercentual,
    decimal? CetCalculadoAaPercentual,
    decimal? ValorTotalEstimadoBrl,
    DateOnly DataCaptura,
    DateOnly? DataValidadeMercado,
    string Status,
    string? MotivoRecusa)
{
    public static PropostaDto From(Proposta p) => new(
        p.Id,
        p.CotacaoId,
        p.BancoId,
        p.MoedaOriginal.ToString(),
        p.ValorOferecidoMoedaOriginal.Valor,
        p.TaxaAaPercentual,
        p.IofPercentual,
        p.SpreadAaPercentual,
        p.PrazoDias,
        p.EstruturaAmortizacao.ToString(),
        p.PeriodicidadeJuros.ToString(),
        p.ExigeNdf,
        p.CustoNdfAaPercentual,
        p.GarantiaExigida,
        p.ValorGarantiaExigidaBrl.Valor,
        p.GarantiaEhCdbCativo,
        p.RendimentoCdbAaPercentual,
        p.CetCalculadoAaPercentual,
        p.ValorTotalEstimadoBrl?.Valor,
        new DateOnly(p.DataCaptura.Year, p.DataCaptura.Month, p.DataCaptura.Day),
        p.DataValidadeMercado.HasValue
            ? new DateOnly(p.DataValidadeMercado.Value.Year, p.DataValidadeMercado.Value.Month, p.DataValidadeMercado.Value.Day)
            : null,
        p.Status.ToString(),
        p.MotivoRecusa);
}
