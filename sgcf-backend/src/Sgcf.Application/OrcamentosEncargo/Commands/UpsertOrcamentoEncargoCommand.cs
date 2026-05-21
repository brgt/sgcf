using MediatR;
using NodaTime;
using Sgcf.Domain.OrcamentosEncargo;

namespace Sgcf.Application.OrcamentosEncargo.Commands;

/// <summary>
/// Cadastra ou atualiza o orçamento de encargo financeiro para a chave composta
/// (ano, mês, tipo_encargo, banco_id, contrato_id).
/// </summary>
/// <param name="Ano">Ano da competência (2000–2100).</param>
/// <param name="Mes">Mês da competência (1–12).</param>
/// <param name="TipoEncargo">Tipo do encargo financeiro (ex.: "JUROS", "IOF").</param>
/// <param name="ValorOrcadoBrl">Valor orçado em BRL. Deve ser não-negativo.</param>
/// <param name="BancoId">Banco vinculado. Opcional.</param>
/// <param name="ContratoId">Contrato vinculado. Opcional.</param>
/// <param name="Observacao">Observação livre. Opcional.</param>
public sealed record UpsertOrcamentoEncargoCommand(
    int Ano,
    int Mes,
    string TipoEncargo,
    decimal ValorOrcadoBrl,
    Guid? BancoId,
    Guid? ContratoId,
    string? Observacao) : IRequest<OrcamentoEncargoDto>;

/// <summary>
/// Handler de upsert: cria quando não existe, atualiza quando já existe,
/// usando a chave composta (ano, mês, tipo_encargo, banco_id, contrato_id).
/// </summary>
public sealed class UpsertOrcamentoEncargoCommandHandler(
    IOrcamentoEncargoRepository repository,
    IClock clock)
    : IRequestHandler<UpsertOrcamentoEncargoCommand, OrcamentoEncargoDto>
{
    public async Task<OrcamentoEncargoDto> Handle(
        UpsertOrcamentoEncargoCommand command,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        OrcamentoEncargo? existente = await repository.GetAsync(
            command.Ano,
            command.Mes,
            command.TipoEncargo,
            command.BancoId,
            command.ContratoId,
            cancellationToken);

        OrcamentoEncargo orcamento;

        if (existente is not null)
        {
            existente.Atualizar(command.ValorOrcadoBrl, command.Observacao, agora);
            orcamento = existente;
        }
        else
        {
            orcamento = OrcamentoEncargo.Criar(
                command.Ano,
                command.Mes,
                command.TipoEncargo,
                command.ValorOrcadoBrl,
                command.BancoId,
                command.ContratoId,
                command.Observacao,
                agora);

            repository.Add(orcamento);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new OrcamentoEncargoDto(
            orcamento.Id,
            orcamento.Ano,
            orcamento.Mes,
            orcamento.TipoEncargo,
            orcamento.ValorOrcadoBrl.Valor,
            orcamento.BancoId,
            orcamento.ContratoId,
            orcamento.Observacao);
    }
}
