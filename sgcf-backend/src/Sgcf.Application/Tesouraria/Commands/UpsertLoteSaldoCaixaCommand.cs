using FluentValidation;
using MediatR;
using NodaTime;
using NodaTime.Text;
using Sgcf.Application.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Commands;

/// <summary>
/// Cria ou atualiza em lote os saldos de caixa para as contas e datas informadas.
/// Em caso de atualização, persiste um evento de auditoria com o valor anterior.
/// </summary>
public sealed record UpsertLoteSaldoCaixaCommand(
    IReadOnlyList<UpsertSaldoCaixaItemDto> Itens)
    : IRequest<IReadOnlyList<SaldoCaixaDto>>;

public sealed class UpsertLoteSaldoCaixaCommandValidator : AbstractValidator<UpsertLoteSaldoCaixaCommand>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;

    public UpsertLoteSaldoCaixaCommandValidator()
    {
        RuleFor(c => c.Itens)
            .NotEmpty()
            .WithMessage("A lista de itens não pode ser vazia.");

        RuleForEach(c => c.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.ContaId)
                .NotEmpty()
                .WithMessage("ContaId não pode ser vazio.");

            item.RuleFor(i => i.DataReferencia)
                .NotEmpty()
                .Must(d => IsoPattern.Parse(d).Success)
                .WithMessage("DataReferencia deve ser uma data ISO válida (yyyy-MM-dd).");

            item.RuleFor(i => i.RegistradoPor)
                .NotEmpty()
                .WithMessage("RegistradoPor não pode ser vazio.");

            item.RuleFor(i => i.Moeda)
                .NotEmpty()
                .Must(m => Enum.TryParse<Moeda>(m, ignoreCase: true, out _))
                .WithMessage($"Moeda deve ser um dos valores: {string.Join(", ", Enum.GetNames<Moeda>())}.");
        });
    }
}

public sealed class UpsertLoteSaldoCaixaCommandHandler(
    ISaldoCaixaRepository saldoRepo,
    IContaBancariaRepository contaRepo,
    IAuditLogWriter auditLog,
    IClock clock)
    : IRequestHandler<UpsertLoteSaldoCaixaCommand, IReadOnlyList<SaldoCaixaDto>>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;

    public async Task<IReadOnlyList<SaldoCaixaDto>> Handle(
        UpsertLoteSaldoCaixaCommand command,
        CancellationToken cancellationToken)
    {
        List<SaldoCaixaDto> resultado = new(command.Itens.Count);

        foreach (UpsertSaldoCaixaItemDto item in command.Itens)
        {
            LocalDate dataRef = IsoPattern.Parse(item.DataReferencia).Value;
            Moeda moeda = Enum.Parse<Moeda>(item.Moeda, ignoreCase: true);
            Money novoValor = new(item.Valor, moeda);

            // Garante que a conta existe e não foi excluída.
            ContaBancaria conta = await contaRepo.GetByIdAsync(item.ContaId, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"ContaBancaria com Id '{item.ContaId}' não encontrada.");

            SaldoCaixa? saldoExistente = await saldoRepo.GetAsync(item.ContaId, dataRef, cancellationToken);

            if (saldoExistente is not null)
            {
                // UPDATE: persiste diff de auditoria com o valor anterior.
                Money valorAntes = saldoExistente.Atualizar(novoValor, item.RegistradoPor, clock);
                await saldoRepo.SaveChangesAsync(cancellationToken);

                await auditLog.WriteAsync(
                    entity: "SaldoCaixa",
                    entityId: saldoExistente.Id,
                    operation: "UPDATE",
                    diff: new
                    {
                        valorAntes = valorAntes.Valor,
                        valorDepois = novoValor.Valor,
                        moeda = moeda.ToString()
                    },
                    ct: cancellationToken);

                resultado.Add(SaldoCaixaDto.From(saldoExistente));
            }
            else
            {
                // CREATE: não registra auditoria para novos saldos (criação rastreada pelo EF interceptor).
                SaldoCaixa novo = SaldoCaixa.Criar(item.ContaId, dataRef, novoValor, item.RegistradoPor, clock);

                await saldoRepo.AddAsync(novo, cancellationToken);
                await saldoRepo.SaveChangesAsync(cancellationToken);

                resultado.Add(SaldoCaixaDto.From(novo));
            }
        }

        return resultado.AsReadOnly();
    }
}
