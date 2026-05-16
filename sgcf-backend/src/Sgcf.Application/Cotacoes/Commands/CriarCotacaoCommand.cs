using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Cria uma nova cotação em estado Rascunho.
/// Se CodigoInterno for nulo, gera automaticamente via repositório.
/// Busca PTAX D-1 via <see cref="ICotacaoFxRepository"/>. SPEC §6.1.
/// </summary>
public sealed record CriarCotacaoCommand(
    string? CodigoInterno,
    string Modalidade,
    decimal ValorAlvoBrl,
    int PrazoMaximoDias,
    DateOnly DataAbertura,
    string? Observacoes = null) : IRequest<CotacaoDto>;

public sealed class CriarCotacaoCommandValidator : AbstractValidator<CriarCotacaoCommand>
{
    public CriarCotacaoCommandValidator()
    {
        RuleFor(c => c.Modalidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");

        RuleFor(c => c.ValorAlvoBrl)
            .GreaterThan(0m)
            .WithMessage("ValorAlvoBrl deve ser maior que zero.");

        RuleFor(c => c.PrazoMaximoDias)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PrazoMaximoDias deve ser maior ou igual a 1.");
    }
}

public sealed class CriarCotacaoCommandHandler(
    ICotacaoRepository repo,
    ICotacaoFxRepository fxRepo,
    IClock clock) : IRequestHandler<CriarCotacaoCommand, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(CriarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        LocalDate dataAbertura = new(cmd.DataAbertura.Year, cmd.DataAbertura.Month, cmd.DataAbertura.Day);

        // PTAX D-1: busca o último registro de PTAX antes da data de abertura
        LocalDate dataPtax = dataAbertura.PlusDays(-1);
        CotacaoFx cotacaoFx = await fxRepo.GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            dataPtax,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"PTAX D-1 não disponível para a data {dataPtax}. Cadastre a cotação USD/BRL antes de criar a cotação.");

        decimal ptax = cotacaoFx.ValorVenda.Valor;

        // Gerar código interno se não informado
        string codigoInterno = cmd.CodigoInterno is not null && !string.IsNullOrWhiteSpace(cmd.CodigoInterno)
            ? cmd.CodigoInterno
            : await repo.GerarProximoCodigoInternoAsync(dataAbertura.Year, cancellationToken);

        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);
        Money valorAlvo = new(cmd.ValorAlvoBrl, Moeda.Brl);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno,
            modalidade,
            valorAlvo,
            cmd.PrazoMaximoDias,
            dataAbertura,
            dataPtaxReferencia: cotacaoFx.Momento.InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date,
            ptaxUsadaUsdBrl: ptax,
            clock,
            cmd.Observacoes);

        repo.Add(cotacao);
        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao);
    }
}
