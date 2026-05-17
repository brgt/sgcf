using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Cria limite operacional para banco/modalidade. SPEC §6.1.</summary>
public sealed record CreateLimiteBancoCommand(
    Guid BancoId,
    string Modalidade,
    decimal ValorLimiteBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim = null,
    string? Observacoes = null,
    IReadOnlyList<CriarGarantiaExigidaLimiteRequest>? GarantiasExigidas = null) : IRequest<LimiteBancoDto>;

public sealed class CreateLimiteBancoCommandValidator : AbstractValidator<CreateLimiteBancoCommand>
{
    public CreateLimiteBancoCommandValidator()
    {
        RuleFor(c => c.BancoId).NotEmpty();

        RuleFor(c => c.Modalidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");

        RuleFor(c => c.ValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("ValorLimiteBrl deve ser maior que zero.");

        // Validate that each Tipo string maps to a known enum value.
        // XOR (percentual×valorFixo) is NOT validated here — domain handles it and
        // surfaces ArgumentException (→ 400) or InvalidOperationException (→ 409).
        RuleForEach(c => c.GarantiasExigidas)
            .ChildRules(g =>
                g.RuleFor(r => r.Tipo)
                 .NotEmpty()
                 .Must(v => Enum.TryParse<TipoGarantia>(v, ignoreCase: true, out _))
                 .WithMessage(r => $"Tipo de garantia inválido: '{r.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}."))
            .When(c => c.GarantiasExigidas is not null);
    }
}

public sealed class CreateLimiteBancoCommandHandler(ILimiteBancoRepository repo, IClock clock)
    : IRequestHandler<CreateLimiteBancoCommand, LimiteBancoDto>
{
    public async Task<LimiteBancoDto> Handle(CreateLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);
        LocalDate inicio = new(cmd.DataVigenciaInicio.Year, cmd.DataVigenciaInicio.Month, cmd.DataVigenciaInicio.Day);
        LocalDate? fim = cmd.DataVigenciaFim.HasValue
            ? new LocalDate(cmd.DataVigenciaFim.Value.Year, cmd.DataVigenciaFim.Value.Month, cmd.DataVigenciaFim.Value.Day)
            : null;

        // GAP-001: rejeitar sobreposição de vigência para o mesmo par bancoId×modalidade.
        LimiteBanco? conflito = await repo.FindOverlappingAsync(
            cmd.BancoId, modalidade, inicio, fim, cancellationToken: cancellationToken);

        if (conflito is not null)
        {
            string fimConflito = conflito.DataVigenciaFim.HasValue
                ? conflito.DataVigenciaFim.Value.ToString("uuuu-MM-dd", null)
                : "em aberto";

            throw new InvalidOperationException(
                $"Já existe o limite '{conflito.Id}' para banco '{cmd.BancoId}' / modalidade '{modalidade}' " +
                $"com vigência de {conflito.DataVigenciaInicio:uuuu-MM-dd} até {fimConflito}, " +
                $"que se sobrepõe ao período solicitado.");
        }

        Money valorLimite = new(cmd.ValorLimiteBrl, Moeda.Brl);

        IEnumerable<GarantiaExigidaLimiteSpec>? specs = cmd.GarantiasExigidas?
            .Select(r => r.ParaSpec());

        LimiteBanco limite = LimiteBanco.Criar(
            cmd.BancoId,
            modalidade,
            valorLimite,
            inicio,
            clock,
            fim,
            cmd.Observacoes,
            garantiasExigidas: specs);

        repo.Add(limite);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteBancoDto.From(limite);
    }
}
