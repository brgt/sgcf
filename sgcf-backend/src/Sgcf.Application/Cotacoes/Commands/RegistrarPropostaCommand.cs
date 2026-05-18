using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Registra proposta recebida de um banco e calcula o CET automaticamente.
/// Para moedas não-USD, obtém cross-rate via <see cref="ICotacaoFxRepository"/>. SPEC §5.1, §6.1.
/// </summary>
public sealed record RegistrarPropostaCommand(
    Guid CotacaoId,
    Guid BancoId,
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

public sealed class RegistrarPropostaCommandValidator : AbstractValidator<RegistrarPropostaCommand>
{
    public RegistrarPropostaCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
        RuleFor(c => c.BancoId).NotEmpty();

        RuleFor(c => c.MoedaOriginal)
            .NotEmpty()
            .Must(v => Enum.TryParse<Moeda>(v, true, out _))
            .WithMessage($"MoedaOriginal deve ser um dos valores: {string.Join(", ", Enum.GetNames<Moeda>())}.");

        RuleFor(c => c.ValorOferecido).GreaterThan(0m).WithMessage("ValorOferecido deve ser maior que zero.");
        RuleFor(c => c.TaxaAa).GreaterThanOrEqualTo(0m).WithMessage("TaxaAa não pode ser negativa.");
        RuleFor(c => c.IofPct).GreaterThanOrEqualTo(0m).WithMessage("IofPct não pode ser negativo.");
        RuleFor(c => c.SpreadAa).GreaterThanOrEqualTo(0m).WithMessage("SpreadAa não pode ser negativo.");
        RuleFor(c => c.PrazoDias).GreaterThanOrEqualTo(1).WithMessage("PrazoDias deve ser maior ou igual a 1.");

        RuleFor(c => c.EstruturaAmortizacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<EstruturaAmortizacao>(v, true, out _))
            .WithMessage($"EstruturaAmortizacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<EstruturaAmortizacao>())}.");

        RuleFor(c => c.PeriodicidadeJuros)
            .NotEmpty()
            .Must(v => Enum.TryParse<Periodicidade>(v, true, out _))
            .WithMessage($"PeriodicidadeJuros deve ser um dos valores: {string.Join(", ", Enum.GetNames<Periodicidade>())}.");

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

public sealed class RegistrarPropostaCommandHandler(
    ICotacaoRepository repo,
    ICotacaoFxRepository fxRepo,
    IClock clock,
    IContratoRepository? contratoRepo = null) : IRequestHandler<RegistrarPropostaCommand, PropostaDto>
{
    public async Task<PropostaDto> Handle(RegistrarPropostaCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        Moeda moeda = Enum.Parse<Moeda>(cmd.MoedaOriginal, true);
        EstruturaAmortizacao estrutura = Enum.Parse<EstruturaAmortizacao>(cmd.EstruturaAmortizacao, true);
        Periodicidade periodicidade = Enum.Parse<Periodicidade>(cmd.PeriodicidadeJuros, true);

        // Onda 1 REFINIMP — SPEC §4.2: proposta deve ter mesma moeda do contrato mãe.
        if (cotacao.Modalidade == ModalidadeContrato.Refinimp && cotacao.ContratoMaeId.HasValue)
        {
            Contrato mae = await (contratoRepo
                ?? throw new InvalidOperationException(
                    "IContratoRepository é obrigatório para registrar proposta em cotação REFINIMP."))
                .GetByIdAsync(cotacao.ContratoMaeId.Value, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Contrato mãe '{cotacao.ContratoMaeId.Value}' não encontrado.");

            if (moeda != mae.Moeda)
            {
                throw new InvalidOperationException(
                    $"Proposta REFINIMP deve ser na mesma moeda do contrato mãe ({mae.Moeda}); " +
                    $"recebida {moeda}.");
            }
        }

        Money valorOferecido = new(cmd.ValorOferecido, moeda);
        Money valorGarantia = new(cmd.ValorGarantiaBrl, Moeda.Brl);

        LocalDate dataCaptura = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        // Para moedas não-USD/BRL: obter cross-rate explícito para uso no CET (SPEC §5.1 e aviso Onda 1)
        decimal ptaxEfetiva = await ObterPtaxEfetivaAsync(moeda, cotacao, fxRepo, cancellationToken);

        // Cria proposta via método do agregado (construtor de Proposta é internal)
        Proposta proposta = cotacao.AdicionarProposta(
            cmd.BancoId,
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

        // Calcular CET e setar cache via método público do agregado
        decimal cet = CalculadoraCet.CalcularCet(proposta, ptaxEfetiva, dataCaptura);
        decimal valorTotalBrl = CalcularValorTotalBrl(moeda, cmd.ValorOferecido, ptaxEfetiva, cmd.TaxaAa, cmd.SpreadAa, cmd.PrazoDias);
        cotacao.AtualizarCetProposta(proposta.Id, cet, new Money(valorTotalBrl, Moeda.Brl));

        await repo.SaveChangesAsync(cancellationToken);

        return PropostaDto.From(proposta);
    }

    // Obtém PTAX efetiva para o cálculo de CET:
    // - BRL: retorna 1.0 (sem conversão)
    // - USD: retorna PtaxUsadaUsdBrl da cotação
    // - EUR/CNY/JPY: busca cross-rate via fxRepo (moeda→USD→BRL via PTAX da cotação)
    internal static async Task<decimal> ObterPtaxEfetivaAsync(
        Moeda moeda,
        Cotacao cotacao,
        ICotacaoFxRepository fxRepo,
        CancellationToken cancellationToken)
    {
        if (moeda == Moeda.Brl)
        {
            return 1m;
        }

        if (moeda == Moeda.Usd)
        {
            // ! null-forgiving seguro: este branch só é atingido para propostas USD,
            // que só existem em cotações FINIMP/REFINIMP/Lei4131. O invariante de domínio
            // garante PtaxUsadaUsdBrl não-null para essas modalidades.
            return cotacao.PtaxUsadaUsdBrl!.Value;
        }

        // Cross-rate: obtém cotação moeda/USD mais recente e multiplica pela PTAX USD/BRL.
        // DataPtaxReferencia é garantidamente não-null aqui (mesma razão — modalidade cambial).
        CotacaoFx crossRate = await fxRepo.GetMaisRecenteAsync(
            moeda,
            TipoCotacao.PtaxD1,
            cotacao.DataPtaxReferencia!.Value,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Cross-rate {moeda}/USD não disponível para {cotacao.DataPtaxReferencia}. " +
                "Cadastre a cotação FX antes de registrar proposta nessa moeda.");

        // crossRate.ValorVenda: moeda base (ex: EUR) expressa em USD
        return Math.Round(crossRate.ValorVenda.Valor * cotacao.PtaxUsadaUsdBrl!.Value, 6, MidpointRounding.AwayFromZero);
    }

    internal static decimal CalcularValorTotalBrl(
        Moeda moeda,
        decimal valorOferecido,
        decimal ptax,
        decimal taxaAa,
        decimal spreadAa,
        int prazoDias)
    {
        decimal principalBrl = moeda == Moeda.Brl
            ? valorOferecido
            : Math.Round(valorOferecido * ptax, 6, MidpointRounding.AwayFromZero);

        decimal taxaTotal = (taxaAa + spreadAa) / 100m;
        return Math.Round(
            principalBrl * (1m + taxaTotal * prazoDias / 360m),
            6,
            MidpointRounding.AwayFromZero);
    }
}
