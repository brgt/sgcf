using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Common;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Encerra o limite indicado e cria o sucessor em uma única transação.
/// O limite atual recebe DataVigenciaFim = NovoInicio - 1 dia.
/// RV-02 — SPEC de reavaliação de crédito.
/// </summary>
public sealed record SubstituirLimiteBancoCommand(
    Guid LimiteId,
    DateOnly NovoInicio,
    decimal NovoValorLimiteBrl,
    DateOnly? NovaDataVigenciaFim = null,
    string? Observacoes = null,
    string? MotivoEncerramento = null,
    IReadOnlyList<CriarGarantiaExigidaItemRequest>? GarantiasExigidas = null)
    : IRequest<LimiteBancoDto>;

public sealed class SubstituirLimiteBancoCommandValidator : AbstractValidator<SubstituirLimiteBancoCommand>
{
    public SubstituirLimiteBancoCommandValidator()
    {
        RuleFor(c => c.LimiteId).NotEmpty();

        RuleFor(c => c.NovoInicio)
            .Must(d => d != DateOnly.MinValue)
            .WithMessage("NovoInicio deve ser uma data válida.");

        RuleFor(c => c.NovoValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("NovoValorLimiteBrl deve ser maior que zero.");

        RuleFor(c => c)
            .Must(c => c.NovaDataVigenciaFim!.Value > c.NovoInicio)
            .When(c => c.NovaDataVigenciaFim.HasValue)
            .WithMessage("NovaDataVigenciaFim deve ser posterior a NovoInicio.");

        RuleForEach(c => c.GarantiasExigidas)
            .ChildRules(g =>
                g.RuleFor(r => r.Tipo)
                 .NotEmpty()
                 .Must(v => Enum.TryParse<TipoGarantia>(v, ignoreCase: true, out _))
                 .WithMessage(r => $"Tipo de garantia inválido: '{r.Tipo}'."))
            .When(c => c.GarantiasExigidas is not null);
    }
}

public sealed class SubstituirLimiteBancoCommandHandler(
    ILimiteBancoRepository repo,
    ILimiteGlobalBancoRepository limiteGlobalRepo,
    IBancoRepository bancoRepo,
    IClock clock)
    : IRequestHandler<SubstituirLimiteBancoCommand, LimiteBancoDto>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<LimiteBancoDto> Handle(SubstituirLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        LimiteBanco anterior = await repo.GetByIdTrackingAsync(cmd.LimiteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{cmd.LimiteId}' não encontrado.");

        // REG-01: banco em regime de limite global puro não admite LimiteBanco por modalidade
        // (defesa em profundidade — consistente com Create/Update).
        Banco? banco = await bancoRepo.GetByIdAsync(anterior.BancoId, cancellationToken);
        if (banco is { RegimeLimite: RegimeLimiteBanco.GlobalPuro })
        {
            throw new InvalidOperationException(
                $"Banco '{banco.Apelido}' opera em regime de limite global e não admite limite por modalidade. [REG-01]");
        }

        LocalDate novoInicio = cmd.NovoInicio.ToLocalDate();

        // RV-02-A: NovoInicio deve ser posterior ao início do limite atual.
        if (novoInicio <= anterior.DataVigenciaInicio)
        {
            throw new ArgumentException(
                $"NovoInicio ({cmd.NovoInicio:uuuu-MM-dd}) deve ser posterior ao início do limite atual ({anterior.DataVigenciaInicio}).");
        }

        // RV-02-D: verificar sobreposição do sucessor.
        LocalDate? novaFimSucessor = cmd.NovaDataVigenciaFim?.ToLocalDate();

        LimiteBanco? conflito = await repo.FindOverlappingAsync(
            anterior.BancoId,
            anterior.Modalidade,
            novoInicio,
            novaFimSucessor,
            excluirId: anterior.Id,
            cancellationToken: cancellationToken);

        if (conflito is not null)
        {
            string fimConflito = conflito.DataVigenciaFim.HasValue
                ? conflito.DataVigenciaFim.Value.ToString("uuuu-MM-dd", null)
                : "em aberto";

            throw new InvalidOperationException(
                $"A vigência do sucessor causa sobreposição com o limite '{conflito.Id}' " +
                $"(vigência: {conflito.DataVigenciaInicio:uuuu-MM-dd} – {fimConflito}). [RV-02-D]");
        }

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        // LG-09: verificar limite global para o novo valor.
        LimiteGlobalBanco? limiteGlobal = await limiteGlobalRepo.GetVigenteByBancoAsync(anterior.BancoId, hoje, cancellationToken);
        if (limiteGlobal is not null)
        {
            Money novoValorVerificacao = new(cmd.NovoValorLimiteBrl, Moeda.Brl);
            if (novoValorVerificacao.MaiorQue(limiteGlobal.ValorLimiteBrl))
            {
                throw new InvalidOperationException(
                    $"O valor do limite por modalidade ({novoValorVerificacao}) não pode superar o limite global vigente do banco ({limiteGlobal.ValorLimiteBrl}). [LG-09]");
            }
        }

        // RV-02-B: encerrar o anterior no dia anterior ao início do sucessor.
        LocalDate dataFimAnterior = novoInicio.PlusDays(-1);
        anterior.Atualizar(clock,
            novaDataVigenciaFim: dataFimAnterior,
            motivoEncerramento: cmd.MotivoEncerramento);
        repo.Update(anterior);

        // RV-02-C: criar o sucessor com os novos parâmetros — sem herdar antecipação.
        IEnumerable<GarantiaExigidaItemSpec>? specs = cmd.GarantiasExigidas?
            .Select(r => r.ParaSpec());

        LimiteBanco sucessor = LimiteBanco.Criar(
            bancoId: anterior.BancoId,
            modalidade: anterior.Modalidade,
            valorLimiteBrl: new Money(cmd.NovoValorLimiteBrl, Moeda.Brl),
            dataVigenciaInicio: novoInicio,
            clock: clock,
            dataVigenciaFim: novaFimSucessor,
            observacoes: cmd.Observacoes,
            garantiasExigidas: specs);

        repo.Add(sucessor);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteBancoDto.From(sucessor);
    }
}
