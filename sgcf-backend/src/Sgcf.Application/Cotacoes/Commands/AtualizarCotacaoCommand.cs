using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cotacoes.Services;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza campos básicos editáveis da cotação (prazo, Observacoes). Apenas em Rascunho. SPEC §7.1.
/// S40 §4.1: aceita tenor {valor, unidade}; prazoMaximoDias permanece como entrada legada.
/// PATCH sem nenhum campo de prazo não altera o prazo (atualização parcial).
/// </summary>
public sealed record AtualizarCotacaoCommand(
    Guid CotacaoId,
    int? PrazoMaximoDias,
    string? Observacoes,
    int? PrazoMaximoValor = null,
    string? PrazoMaximoUnidade = null) : IRequest<CotacaoDto>;

public sealed class AtualizarCotacaoCommandValidator : AbstractValidator<AtualizarCotacaoCommand>
{
    public AtualizarCotacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();

        When(c => c.PrazoMaximoDias.HasValue, () =>
            RuleFor(c => c.PrazoMaximoDias!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PrazoMaximoDias deve ser maior ou igual a 1."));

        When(c => c.PrazoMaximoValor.HasValue, () =>
            RuleFor(c => c.PrazoMaximoValor!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PrazoMaximoValor deve ser maior ou igual a 1."));

        When(c => c.PrazoMaximoUnidade is not null, () =>
            RuleFor(c => c.PrazoMaximoUnidade!)
                .Must(u => Enum.TryParse<UnidadePrazo>(u, true, out _))
                .WithMessage($"PrazoMaximoUnidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<UnidadePrazo>())}."));
    }
}

public sealed class AtualizarCotacaoCommandHandler(ICotacaoRepository repo, IClock clock)
    : IRequestHandler<AtualizarCotacaoCommand, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(AtualizarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        List<AlertaDto> alertas = [];

        if (cmd.PrazoMaximoValor.HasValue)
        {
            // Tenor estruturado prevalece e recalcula o dia canônico (30/360). SPEC S40 §4.1.
            UnidadePrazo? unidade = cmd.PrazoMaximoUnidade is null
                ? null
                : Enum.Parse<UnidadePrazo>(cmd.PrazoMaximoUnidade, true);
            ResolvedorTenor.Resultado tenor = ResolvedorTenor.Resolver(
                cotacao.Modalidade, cmd.PrazoMaximoValor, unidade, cmd.PrazoMaximoDias);

            cotacao.EditarTenor(tenor.Valor, tenor.Unidade, clock);
            if (tenor.Alerta is not null)
            {
                alertas.Add(tenor.Alerta);
            }

            if (cmd.Observacoes is not null)
            {
                cotacao.EditarCamposBasicos(null, cmd.Observacoes, clock);
            }
        }
        else
        {
            // Caminho legado/parcial: edita dias (se enviado) e/ou observações.
            cotacao.EditarCamposBasicos(cmd.PrazoMaximoDias, cmd.Observacoes, clock);
        }

        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao, alertas);
    }
}
