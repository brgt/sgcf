using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos.Commands;

/// <summary>
/// Uma parcela fornecida manualmente para importação no cronograma de Balcão Caixa.
/// </summary>
public sealed record ParcelaManualRequest(
    DateOnly DataVencimento,
    decimal ValorPrincipal,
    decimal ValorJuros);

/// <summary>
/// Importa um cronograma manual para contratos Balcão Caixa.
/// Substitui integralmente qualquer cronograma existente.
/// </summary>
public sealed record ImportarCronogramaCommand(
    Guid ContratoId,
    IReadOnlyList<ParcelaManualRequest> Parcelas)
    : IRequest<IReadOnlyList<EventoCronogramaDto>>;

public sealed class ImportarCronogramaCommandHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo)
    : IRequestHandler<ImportarCronogramaCommand, IReadOnlyList<EventoCronogramaDto>>
{
    public async Task<IReadOnlyList<EventoCronogramaDto>> Handle(
        ImportarCronogramaCommand cmd,
        CancellationToken cancellationToken)
    {
        Contrato contrato = await contratoRepo.GetByIdAsync(cmd.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{cmd.ContratoId}' não encontrado.");

        if (contrato.Modalidade != ModalidadeContrato.BalcaoCaixa)
        {
            throw new InvalidOperationException(
                $"Importação manual de cronograma é exclusiva para contratos Balcão Caixa. " +
                $"Modalidade atual: {contrato.Modalidade}.");
        }

        if (cmd.Parcelas.Count == 0)
        {
            throw new ArgumentException("A lista de parcelas não pode ser vazia.", nameof(cmd));
        }

        await cronogramaRepo.DeleteAllByContratoIdAsync(cmd.ContratoId, cancellationToken);

        List<EventoCronograma> entities = new(cmd.Parcelas.Count * 2);
        short numeroEvento = 1;

        foreach (ParcelaManualRequest parcela in cmd.Parcelas)
        {
            LocalDate dataPrevista = new(parcela.DataVencimento.Year, parcela.DataVencimento.Month, parcela.DataVencimento.Day);
            Money valorPrincipal = new(parcela.ValorPrincipal, contrato.ValorPrincipal.Moeda);
            Money valorJuros = new(parcela.ValorJuros, contrato.ValorPrincipal.Moeda);

            // Juros event first — mirrors convention used in BulletStrategy
            entities.Add(EventoCronograma.Criar(
                contratoId: cmd.ContratoId,
                numeroEvento: numeroEvento,
                tipo: TipoEventoCronograma.Juros,
                dataPrevista: dataPrevista,
                valorMoedaOriginal: valorJuros,
                saldoDevedorApos: null));

            entities.Add(EventoCronograma.Criar(
                contratoId: cmd.ContratoId,
                numeroEvento: numeroEvento,
                tipo: TipoEventoCronograma.Principal,
                dataPrevista: dataPrevista,
                valorMoedaOriginal: valorPrincipal,
                saldoDevedorApos: null));

            numeroEvento++;
        }

        cronogramaRepo.AddRange(entities);
        await cronogramaRepo.SaveChangesAsync(cancellationToken);

        List<EventoCronogramaDto> result = new(entities.Count);
        foreach (EventoCronograma e in entities)
        {
            result.Add(new EventoCronogramaDto(
                NumeroEvento: e.NumeroEvento,
                Tipo: e.Tipo.ToString(),
                DataPrevista: new DateOnly(e.DataPrevista.Year, e.DataPrevista.Month, e.DataPrevista.Day),
                Valor: e.ValorMoedaOriginal.Valor,
                Moeda: e.ValorMoedaOriginal.Moeda.ToString(),
                SaldoDevedorApos: e.SaldoDevedorApos?.Valor,
                Status: e.Status.ToString(),
                DataPagamentoEfetivo: null,
                ValorPagamentoEfetivo: null));
        }

        return result.AsReadOnly();
    }
}
