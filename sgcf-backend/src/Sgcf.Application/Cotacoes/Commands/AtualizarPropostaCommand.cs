using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza proposta existente removendo-a e criando uma nova, e recalcula CET.
/// Proposta deve estar em status Recebida (não aceita/recusada). SPEC §6.1.
/// Versionamento via audit_log (SPEC §11.7).
/// </summary>
public sealed record AtualizarPropostaCommand(
    Guid CotacaoId,
    Guid PropostaId,
    string MoedaOriginal,
    decimal ValorOferecido,
    decimal TaxaAa,
    decimal IofPct,
    decimal SpreadAa,
    int PrazoDias,
    string EstruturaAmortizacao,
    string PeriodicidadeJuros,
    bool ExigeNdf,
    decimal? CustoNdfAa,
    string GarantiaExigida,
    decimal ValorGarantiaBrl,
    bool GarantiaEhCdbCativo,
    decimal? RendimentoCdbAa) : IRequest<PropostaDto>;

public sealed class AtualizarPropostaCommandValidator : AbstractValidator<AtualizarPropostaCommand>
{
    public AtualizarPropostaCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
        RuleFor(c => c.PropostaId).NotEmpty();
        RuleFor(c => c.ValorOferecido).GreaterThan(0m);
        RuleFor(c => c.TaxaAa).GreaterThanOrEqualTo(0m);
        RuleFor(c => c.PrazoDias).GreaterThanOrEqualTo(1);

        RuleFor(c => c.CustoNdfAa)
            .NotNull()
            .When(c => c.ExigeNdf)
            .WithMessage("CustoNdfAa é obrigatório quando ExigeNdf = true.");

        RuleFor(c => c.RendimentoCdbAa)
            .NotNull()
            .When(c => c.GarantiaEhCdbCativo)
            .WithMessage("RendimentoCdbAa é obrigatório quando GarantiaEhCdbCativo = true.");
    }
}

public sealed class AtualizarPropostaCommandHandler(
    ICotacaoRepository repo,
    ICotacaoFxRepository fxRepo) : IRequestHandler<AtualizarPropostaCommand, PropostaDto>
{
    public async Task<PropostaDto> Handle(AtualizarPropostaCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        // Localizar proposta existente no agregado
        Proposta propostaExistente = cotacao.Propostas.FirstOrDefault(p => p.Id == cmd.PropostaId)
            ?? throw new KeyNotFoundException($"Proposta '{cmd.PropostaId}' não encontrada na cotação '{cmd.CotacaoId}'.");

        if (propostaExistente.Status != StatusProposta.Recebida)
        {
            throw new InvalidOperationException(
                $"Proposta '{cmd.PropostaId}' não pode ser editada — status atual: '{propostaExistente.Status}'.");
        }

        Moeda moeda = Enum.Parse<Moeda>(cmd.MoedaOriginal, true);
        EstruturaAmortizacao estrutura = Enum.Parse<EstruturaAmortizacao>(cmd.EstruturaAmortizacao, true);
        Periodicidade periodicidade = Enum.Parse<Periodicidade>(cmd.PeriodicidadeJuros, true);

        Money valorOferecido = new(cmd.ValorOferecido, moeda);
        Money valorGarantia = new(cmd.ValorGarantiaBrl, Moeda.Brl);

        // Mantém data original de captura; adiciona nova proposta com dados atualizados
        LocalDate dataCaptura = propostaExistente.DataCaptura;

        decimal ptaxEfetiva = await RegistrarPropostaCommandHandler.ObterPtaxEfetivaAsync(
            moeda, cotacao, fxRepo, cancellationToken);

        Proposta novaProsta = cotacao.AdicionarProposta(
            propostaExistente.BancoId,
            moeda,
            valorOferecido,
            cmd.TaxaAa,
            cmd.IofPct,
            cmd.SpreadAa,
            cmd.PrazoDias,
            estrutura,
            periodicidade,
            cmd.ExigeNdf,
            cmd.CustoNdfAa,
            cmd.GarantiaExigida,
            valorGarantia,
            cmd.GarantiaEhCdbCativo,
            cmd.RendimentoCdbAa,
            dataCaptura);

        decimal cet = CalculadoraCet.CalcularCet(novaProsta, ptaxEfetiva, dataCaptura);
        decimal valorTotalBrl = RegistrarPropostaCommandHandler.CalcularValorTotalBrl(
            moeda, cmd.ValorOferecido, ptaxEfetiva, cmd.TaxaAa, cmd.SpreadAa, cmd.PrazoDias);

        cotacao.AtualizarCetProposta(novaProsta.Id, cet, new Money(valorTotalBrl, Moeda.Brl));

        await repo.SaveChangesAsync(cancellationToken);

        return PropostaDto.From(novaProsta);
    }
}
