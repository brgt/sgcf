using FluentValidation;
using MediatR;
using NodaTime;

using Sgcf.Application.Simulacao.Cache;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Atualiza todos os campos mutáveis de uma simulação de contratação existente.
/// Version é incrementado automaticamente pelo domínio (AD-3) para invalidar cache Redis.
/// Permitido em Rascunho e Ativo. Bloqueado em Arquivado.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir.
/// Lança <see cref="InvalidOperationException"/> se Arquivado ou simulação não encontrada.
/// SPEC §7.5.
/// </summary>
public sealed record AtualizarSimulacaoCommand(
    Guid CenarioId,
    Guid SimulacaoId,
    AtualizarSimulacaoInput Input) : IRequest<CenarioSimulacaoDto>;

public sealed class AtualizarSimulacaoCommandValidator : AbstractValidator<AtualizarSimulacaoCommand>
{
    public AtualizarSimulacaoCommandValidator()
    {
        RuleFor(c => c.CenarioId).NotEmpty();
        RuleFor(c => c.SimulacaoId).NotEmpty();

        RuleFor(c => c.Input.ValorPrincipal)
            .GreaterThan(0m)
            .WithMessage("ValorPrincipal deve ser maior que zero.");

        RuleFor(c => c.Input.QuantidadeParcelas)
            .GreaterThanOrEqualTo(1)
            .WithMessage("QuantidadeParcelas deve ser no mínimo 1.");

        RuleFor(c => c.Input.TipoTaxa)
            .NotEmpty()
            .Must(v => Enum.TryParse<TipoTaxa>(v, true, out _))
            .WithMessage($"TipoTaxa inválido. Valores: {string.Join(", ", Enum.GetNames<TipoTaxa>())}.");

        RuleFor(c => c.Input.BaseCalculo)
            .NotEmpty()
            .Must(v => Enum.TryParse<BaseCalculo>(v, true, out _))
            .WithMessage($"BaseCalculo inválido.");

        RuleFor(c => c.Input.EstruturaAmortizacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<EstruturaAmortizacao>(v, true, out _))
            .WithMessage($"EstruturaAmortizacao inválida.");

        RuleFor(c => c.Input.Periodicidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<Periodicidade>(v, true, out _))
            .WithMessage($"Periodicidade inválida.");

        RuleFor(c => c.Input.AnchorDiaMes)
            .NotEmpty()
            .Must(v => Enum.TryParse<AnchorDiaMes>(v, true, out _))
            .WithMessage($"AnchorDiaMes inválido.");
    }
}

public sealed class AtualizarSimulacaoCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock,
    ICronogramaSimulacaoCache cache) : IRequestHandler<AtualizarSimulacaoCommand, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        AtualizarSimulacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        AtualizarSimulacaoInput i = cmd.Input;
        Moeda moeda = Enum.Parse<Moeda>(i.Moeda, true);
        TipoTaxa tipoTaxa = Enum.Parse<TipoTaxa>(i.TipoTaxa, true);
        BaseCalculo baseCalculo = Enum.Parse<BaseCalculo>(i.BaseCalculo, true);
        EstruturaAmortizacao estrutura = Enum.Parse<EstruturaAmortizacao>(i.EstruturaAmortizacao, true);
        Periodicidade periodicidade = Enum.Parse<Periodicidade>(i.Periodicidade, true);
        AnchorDiaMes anchor = Enum.Parse<AnchorDiaMes>(i.AnchorDiaMes, true);

        Money valorPrincipal = new(i.ValorPrincipal, moeda);
        Percentual? taxaAa = i.TaxaAa.HasValue ? Percentual.De(i.TaxaAa.Value) : null;
        Percentual? spreadAa = i.SpreadAa.HasValue ? Percentual.De(i.SpreadAa.Value) : null;

        LocalDate dataContratacao = new(i.DataContratacaoPrevista.Year, i.DataContratacaoPrevista.Month, i.DataContratacaoPrevista.Day);
        LocalDate dataPrimeiroVencimento = new(i.DataPrimeiroVencimento.Year, i.DataPrimeiroVencimento.Month, i.DataPrimeiroVencimento.Day);

        // Domínio incrementa Version internamente (AD-3) e valida invariantes.
        cenario.AtualizarSimulacao(cmd.SimulacaoId, simulacao =>
        {
            simulacao.Atualizar(
                valorPrincipal,
                dataContratacao,
                dataPrimeiroVencimento,
                tipoTaxa,
                taxaAa,
                spreadAa,
                baseCalculo,
                estrutura,
                periodicidade,
                i.QuantidadeParcelas,
                anchor,
                i.AnchorDiaFixo,
                i.GarantiaExigidaPrevista,
                i.Observacoes,
                clock);
        }, clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        // Invalida todas as versões em cache desta simulação. O Version foi incrementado
        // pelo domínio (AD-3), mas clientes com v=N-1 ainda acertariam entradas velhas
        // sem esta invalidação explícita.
        await cache.InvalidarPorSimulacaoAsync(cmd.CenarioId, cmd.SimulacaoId, cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
