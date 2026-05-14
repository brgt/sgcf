using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

public sealed record LancamentoContabilDto(
    Guid Id,
    Guid ContratoId,
    DateOnly Data,
    string Origem,
    decimal Valor,
    string Moeda,
    string Descricao,
    DateTimeOffset CriadoEm)
{
    public static LancamentoContabilDto From(LancamentoContabil lancamento) =>
        new(
            lancamento.Id,
            lancamento.ContratoId,
            new DateOnly(lancamento.Data.Year, lancamento.Data.Month, lancamento.Data.Day),
            lancamento.Origem,
            lancamento.Valor.Valor,
            lancamento.Valor.Moeda.ToString(),
            lancamento.Descricao,
            lancamento.CriadoEm.ToDateTimeOffset());
}
