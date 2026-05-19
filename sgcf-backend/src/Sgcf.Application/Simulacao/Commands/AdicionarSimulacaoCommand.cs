using FluentValidation;
using MediatR;
using NodaTime;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Commands;

/// <summary>
/// Adiciona uma nova simulação de contratação ao cenário.
/// Permitido em Rascunho e Ativo. Bloqueado em Arquivado.
/// Invariantes I-1..I-11 são verificadas pelo factory <see cref="SimulacaoContratacao.Criar"/>.
/// Lança <see cref="KeyNotFoundException"/> se o cenário não existir.
/// Lança <see cref="InvalidOperationException"/> se o cenário estiver Arquivado ou uma invariante for violada.
/// SPEC §7.5.
/// </summary>
public sealed record AdicionarSimulacaoCommand(
    Guid CenarioId,
    AdicionarSimulacaoInput Input) : IRequest<CenarioSimulacaoDto>;

public sealed class AdicionarSimulacaoCommandValidator : AbstractValidator<AdicionarSimulacaoCommand>
{
    public AdicionarSimulacaoCommandValidator()
    {
        RuleFor(c => c.CenarioId).NotEmpty();

        RuleFor(c => c.Input.BancoId).NotEmpty();

        RuleFor(c => c.Input.Modalidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade inválida. Valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");

        RuleFor(c => c.Input.Moeda)
            .NotEmpty()
            .Must(v => Enum.TryParse<Moeda>(v, true, out _))
            .WithMessage($"Moeda inválida. Valores: {string.Join(", ", Enum.GetNames<Moeda>())}.");

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
            .WithMessage($"BaseCalculo inválido. Valores: {string.Join(", ", Enum.GetNames<BaseCalculo>())}.");

        RuleFor(c => c.Input.EstruturaAmortizacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<EstruturaAmortizacao>(v, true, out _))
            .WithMessage($"EstruturaAmortizacao inválida. Valores: {string.Join(", ", Enum.GetNames<EstruturaAmortizacao>())}.");

        RuleFor(c => c.Input.Periodicidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<Periodicidade>(v, true, out _))
            .WithMessage($"Periodicidade inválida. Valores: {string.Join(", ", Enum.GetNames<Periodicidade>())}.");

        RuleFor(c => c.Input.AnchorDiaMes)
            .NotEmpty()
            .Must(v => Enum.TryParse<AnchorDiaMes>(v, true, out _))
            .WithMessage($"AnchorDiaMes inválido. Valores: {string.Join(", ", Enum.GetNames<AnchorDiaMes>())}.");
    }
}

public sealed class AdicionarSimulacaoCommandHandler(
    ICenarioSimulacaoRepository repo,
    IClock clock) : IRequestHandler<AdicionarSimulacaoCommand, CenarioSimulacaoDto>
{
    /// <inheritdoc/>
    public async Task<CenarioSimulacaoDto> Handle(
        AdicionarSimulacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        CenarioSimulacao cenario = await repo.GetByIdAsync(cmd.CenarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cenário '{cmd.CenarioId}' não encontrado.");

        AdicionarSimulacaoInput i = cmd.Input;
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(i.Modalidade, true);
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

        SimulacaoContratacao simulacao = SimulacaoContratacao.Criar(
            cenario.Id,
            i.BancoId,
            modalidade,
            moeda,
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
            clock,
            anoBase: cenario.AnoBase);

        cenario.AdicionarSimulacao(simulacao, clock);

        repo.Update(cenario);
        await repo.SaveChangesAsync(cancellationToken);

        return CenarioSimulacaoDto.From(cenario);
    }
}
