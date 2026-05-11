using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge.Commands;

public sealed class AddHedgeCommandHandler(
    IContratoRepository contratoRepo,
    IHedgeRepository hedgeRepo,
    IClock clock)
    : IRequestHandler<AddHedgeCommand, HedgeDto>
{
    public async Task<HedgeDto> Handle(AddHedgeCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TipoHedge>(command.Tipo, ignoreCase: true, out TipoHedge tipo))
        {
            throw new ArgumentException(
                $"TipoHedge inválido: '{command.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoHedge>())}.");
        }

        if (!Enum.TryParse<Moeda>(command.MoedaBase, ignoreCase: true, out Moeda moeda))
        {
            throw new ArgumentException(
                $"Moeda inválida: '{command.MoedaBase}'. Valores aceitos: {string.Join(", ", Enum.GetNames<Moeda>())}.");
        }

        Contrato contrato = await contratoRepo.GetByIdAsync(command.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{command.ContratoId}' não encontrado.");

        if (moeda != contrato.Moeda)
        {
            throw new ArgumentException(
                $"A moeda do hedge ({moeda}) deve coincidir com a moeda do contrato ({contrato.Moeda}).");
        }

        if (command.NotionalMoedaOriginal > contrato.ValorPrincipal.Valor)
        {
            throw new ArgumentException(
                $"O notional do hedge ({command.NotionalMoedaOriginal}) não pode exceder o valor principal do contrato ({contrato.ValorPrincipal.Valor}).");
        }

        if (command.DataVencimento > contrato.DataVencimento)
        {
            throw new ArgumentException(
                $"A data de vencimento do hedge ({command.DataVencimento}) não pode ser posterior ao vencimento do contrato ({contrato.DataVencimento}).");
        }

        ValidarStrikes(tipo, command);

        Money notional = new(command.NotionalMoedaOriginal, moeda);

        InstrumentoHedge hedge = tipo == TipoHedge.NdfForward
            ? InstrumentoHedge.CriarForward(
                command.ContratoId,
                command.ContraparteId,
                notional,
                command.DataContratacao,
                command.DataVencimento,
                command.StrikeForward!.Value,
                clock)
            : InstrumentoHedge.CriarCollar(
                command.ContratoId,
                command.ContraparteId,
                notional,
                command.DataContratacao,
                command.DataVencimento,
                command.StrikePut!.Value,
                command.StrikeCall!.Value,
                clock);

        hedgeRepo.Add(hedge);
        await hedgeRepo.SaveChangesAsync(cancellationToken);

        IReadOnlyList<InstrumentoHedge> hedgesExistentes =
            await hedgeRepo.ListByContratoAsync(command.ContratoId, cancellationToken);

        List<string> alertas = new();

        // O hedge recém-criado já está salvo, então >1 significa duplicata
        if (hedgesExistentes.Count > 1)
        {
            alertas.Add("Atenção: contrato já possui NDF vinculado.");
        }

        return ToDto(hedge, alertas.AsReadOnly());
    }

    private static void ValidarStrikes(TipoHedge tipo, AddHedgeCommand command)
    {
        if (tipo == TipoHedge.NdfForward && !command.StrikeForward.HasValue)
        {
            throw new ArgumentException("StrikeForward é obrigatório para NdfForward.");
        }

        if (tipo == TipoHedge.NdfCollar)
        {
            if (!command.StrikePut.HasValue)
            {
                throw new ArgumentException("StrikePut é obrigatório para NdfCollar.");
            }

            if (!command.StrikeCall.HasValue)
            {
                throw new ArgumentException("StrikeCall é obrigatório para NdfCollar.");
            }
        }
    }

    private static HedgeDto ToDto(InstrumentoHedge hedge, IReadOnlyList<string> alertas) =>
        new(
            Id: hedge.Id,
            ContratoId: hedge.ContratoId,
            Tipo: hedge.Tipo.ToString(),
            ContraparteId: hedge.ContraparteId,
            NotionalMoedaOriginal: hedge.Notional.Valor,
            MoedaBase: hedge.MoedaBase.ToString().ToUpperInvariant(),
            DataContratacao: hedge.DataContratacao.ToString(),
            DataVencimento: hedge.DataVencimento.ToString(),
            StrikeForward: hedge.StrikeForward,
            StrikePut: hedge.StrikePut,
            StrikeCall: hedge.StrikeCall,
            Status: hedge.Status.ToString(),
            Alertas: alertas);
}
