using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Commands;

public sealed record FinimpDetailRequest(
    string? RofNumero,
    DateOnly? RofDataEmissao,
    string? ExportadorNome,
    string? ExportadorPais,
    string? ProdutoImportado,
    string? FaturaReferencia,
    string? Incoterm,
    decimal? BreakFundingFeePercentual,
    bool TemMarketFlex);

public sealed record Lei4131DetailRequest(
    string? SblcNumero,
    string? SblcBancoEmissor,
    decimal? SblcValorUsd,
    bool TemMarketFlex,
    decimal? BreakFundingFeePercentual);

/// <summary>
/// Dados complementares obrigatórios para contratos da modalidade REFINIMP.
/// <c>PercentualRefinanciado</c> é fornecido como valor "humano" (ex: 70 para 70%).
/// </summary>
public sealed record RefinimpDetailRequest(
    Guid ContratoMaeId,
    decimal PercentualRefinanciado);

public sealed record NceDetailRequest(
    string? NceNumero,
    DateOnly? DataEmissao,
    string? BancoMandatario);

public sealed record BalcaoCaixaDetailRequest(
    string? NumeroOperacao,
    string? TipoProduto,
    bool TemFgi);

public sealed record FgiDetailRequest(
    string? NumeroOperacaoFgi,
    decimal? TaxaFgiAaPct,
    decimal? PercentualCobertoPct);

public sealed record CreateContratoCommand(
    string NumeroExterno,
    Guid BancoId,
    string Modalidade,
    string Moeda,
    decimal ValorPrincipal,
    DateOnly DataContratacao,
    DateOnly DataVencimento,
    decimal TaxaAa,
    string BaseCalculo,
    Guid? ContratoPaiId,
    string? Observacoes,
    FinimpDetailRequest? FinimpDetail,
    Lei4131DetailRequest? Lei4131Detail,
    RefinimpDetailRequest? RefinimpDetail,
    NceDetailRequest? NceDetail = null,
    BalcaoCaixaDetailRequest? BalcaoCaixaDetail = null,
    FgiDetailRequest? FgiDetail = null)
    : IRequest<ContratoDto>;

public sealed class CreateContratoCommandValidator : AbstractValidator<CreateContratoCommand>
{
    public CreateContratoCommandValidator()
    {
        RuleFor(c => c.NumeroExterno)
            .NotEmpty()
            .WithMessage("NumeroExterno não pode ser vazio.");

        RuleFor(c => c.BancoId)
            .NotEmpty()
            .WithMessage("BancoId não pode ser vazio.");

        RuleFor(c => c.Moeda)
            .NotEmpty()
            .Must(v => Enum.TryParse<Domain.Common.Moeda>(v, true, out _))
            .WithMessage($"Moeda deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Common.Moeda>())}.");

        RuleFor(c => c.Modalidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");

        RuleFor(c => c.TaxaAa)
            .GreaterThan(0m)
            .WithMessage("TaxaAa deve ser maior que zero.");

        RuleFor(c => c.DataVencimento)
            .GreaterThan(c => c.DataContratacao)
            .WithMessage("DataVencimento deve ser posterior a DataContratacao.");

        RuleFor(c => c.FinimpDetail)
            .NotNull()
            .When(c => string.Equals(c.Modalidade, nameof(ModalidadeContrato.Finimp), StringComparison.OrdinalIgnoreCase))
            .WithMessage("FinimpDetail é obrigatório para contratos da modalidade Finimp.");

        RuleFor(c => c.Lei4131Detail)
            .NotNull()
            .When(c => string.Equals(c.Modalidade, nameof(ModalidadeContrato.Lei4131), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Lei4131Detail é obrigatório para contratos da modalidade Lei4131.");

        When(c => string.Equals(c.Modalidade, nameof(ModalidadeContrato.Refinimp), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(c => c.ContratoPaiId)
                .NotNull()
                .WithMessage("ContratoPaiId é obrigatório para contratos da modalidade Refinimp.");

            RuleFor(c => c.RefinimpDetail)
                .NotNull()
                .WithMessage("RefinimpDetail é obrigatório para contratos da modalidade Refinimp.");

            RuleFor(c => c.RefinimpDetail!.ContratoMaeId)
                .NotEmpty()
                .When(c => c.RefinimpDetail is not null)
                .WithMessage("RefinimpDetail.ContratoMaeId não pode ser vazio.");
        });

        When(c => string.Equals(c.Modalidade, nameof(ModalidadeContrato.Nce), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(c => c.Moeda)
                .Must(v => string.Equals(v, nameof(Domain.Common.Moeda.Brl), StringComparison.OrdinalIgnoreCase))
                .WithMessage("NCE deve ter moeda BRL.");
        });
    }
}

public sealed class CreateContratoCommandHandler(IContratoRepository repo, IClock clock, IBancoRepository bancoRepo)
    : IRequestHandler<CreateContratoCommand, ContratoDto>
{
    public async Task<ContratoDto> Handle(CreateContratoCommand cmd, CancellationToken cancellationToken)
    {
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);
        Domain.Common.Moeda moeda = Enum.Parse<Domain.Common.Moeda>(cmd.Moeda, true);
        Domain.Common.BaseCalculo baseCalculo = Enum.Parse<Domain.Common.BaseCalculo>(cmd.BaseCalculo, true);

        Money valorPrincipal = new(cmd.ValorPrincipal, moeda);
        Percentual taxaAa = Percentual.De(cmd.TaxaAa);
        LocalDate dataContratacao = new LocalDate(cmd.DataContratacao.Year, cmd.DataContratacao.Month, cmd.DataContratacao.Day);
        LocalDate dataVencimento = new LocalDate(cmd.DataVencimento.Year, cmd.DataVencimento.Month, cmd.DataVencimento.Day);

        Banco banco = await bancoRepo.GetByIdAsync(cmd.BancoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco com Id '{cmd.BancoId}' não encontrado.");
        if (modalidade == ModalidadeContrato.Refinimp && !banco.AceitaRefinimp)
        {
            throw new InvalidOperationException($"O banco '{banco.Apelido}' não aceita contratos Refinimp.");
        }

        Contrato contrato = Contrato.Criar(
            cmd.NumeroExterno,
            cmd.BancoId,
            modalidade,
            valorPrincipal,
            dataContratacao,
            dataVencimento,
            taxaAa,
            baseCalculo,
            clock,
            cmd.ContratoPaiId,
            cmd.Observacoes);

        int count = await repo.CountByAnoAsync(dataContratacao.Year, cancellationToken);
        string codigoInterno = $"FIN-{dataContratacao.Year}-{count + 1:D4}";
        contrato.SetCodigoInterno(codigoInterno);

        repo.Add(contrato);

        FinimpDetail? finimpDetail = null;
        if (modalidade == ModalidadeContrato.Finimp && cmd.FinimpDetail is not null)
        {
            FinimpDetailRequest req = cmd.FinimpDetail;
            LocalDate? rofDataEmissao = req.RofDataEmissao.HasValue
                ? new LocalDate(req.RofDataEmissao.Value.Year, req.RofDataEmissao.Value.Month, req.RofDataEmissao.Value.Day)
                : (LocalDate?)null;

            finimpDetail = FinimpDetail.Criar(
                contrato.Id,
                req.RofNumero,
                rofDataEmissao,
                req.ExportadorNome,
                req.ExportadorPais,
                req.ProdutoImportado,
                req.FaturaReferencia,
                req.Incoterm,
                req.BreakFundingFeePercentual,
                req.TemMarketFlex,
                clock);

            repo.AddFinimpDetail(finimpDetail);
        }

        Lei4131Detail? lei4131Detail = null;
        if (modalidade == ModalidadeContrato.Lei4131 && cmd.Lei4131Detail is not null)
        {
            Lei4131DetailRequest req4131 = cmd.Lei4131Detail;
            lei4131Detail = Lei4131Detail.Criar(
                contrato.Id,
                req4131.SblcNumero,
                req4131.SblcBancoEmissor,
                req4131.SblcValorUsd,
                req4131.TemMarketFlex,
                req4131.BreakFundingFeePercentual,
                clock);

            repo.AddLei4131Detail(lei4131Detail);
        }

        RefinimpDetail? refinimpDetail = null;
        if (modalidade == ModalidadeContrato.Refinimp && cmd.RefinimpDetail is not null)
        {
            refinimpDetail = await ProcessarRefinimpAsync(
                contrato, banco, cmd.RefinimpDetail, valorPrincipal, cancellationToken);
        }

        NceDetail? nceDetail = null;
        if (modalidade == ModalidadeContrato.Nce)
        {
            NceDetailRequest reqNce = cmd.NceDetail ?? new NceDetailRequest(null, null, null);
            nceDetail = NceDetail.Criar(
                contrato.Id,
                reqNce.NceNumero,
                reqNce.DataEmissao.HasValue
                    ? new NodaTime.LocalDate(reqNce.DataEmissao.Value.Year, reqNce.DataEmissao.Value.Month, reqNce.DataEmissao.Value.Day)
                    : (NodaTime.LocalDate?)null,
                reqNce.BancoMandatario,
                clock);

            repo.AddNceDetail(nceDetail);
        }

        BalcaoCaixaDetail? balcaoCaixaDetail = null;
        if (modalidade == ModalidadeContrato.BalcaoCaixa)
        {
            BalcaoCaixaDetailRequest reqBc = cmd.BalcaoCaixaDetail ?? new BalcaoCaixaDetailRequest(null, null, false);
            balcaoCaixaDetail = BalcaoCaixaDetail.Criar(
                contrato.Id,
                reqBc.NumeroOperacao,
                reqBc.TipoProduto,
                reqBc.TemFgi,
                clock);

            repo.AddBalcaoCaixaDetail(balcaoCaixaDetail);
        }

        FgiDetail? fgiDetail = null;
        if (modalidade == ModalidadeContrato.Fgi)
        {
            FgiDetailRequest reqFgi = cmd.FgiDetail ?? new FgiDetailRequest(null, null, null);
            fgiDetail = FgiDetail.Criar(
                contrato.Id,
                reqFgi.NumeroOperacaoFgi,
                reqFgi.TaxaFgiAaPct,
                reqFgi.PercentualCobertoPct,
                clock);

            repo.AddFgiDetail(fgiDetail);
        }

        await repo.SaveChangesAsync(cancellationToken);

        return ContratoDto.From(contrato, finimpDetail, lei4131Detail, refinimpDetail, nceDetail, balcaoCaixaDetail, fgiDetail);
    }

    private static readonly string CodigoCompesBancoDoBrasil = "001";

    private async Task<RefinimpDetail> ProcessarRefinimpAsync(
        Contrato contrato,
        Banco banco,
        RefinimpDetailRequest req,
        Money valorPrincipal,
        CancellationToken cancellationToken)
    {
        // 1. Validate parent (contrato mãe) exists
        Contrato contratoPai = await repo.GetByIdAsync(req.ContratoMaeId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Contrato mãe com Id '{req.ContratoMaeId}' não encontrado.");

        // 2. Validate moeda matches
        if (valorPrincipal.Moeda != contratoPai.Moeda)
        {
            throw new InvalidOperationException(
                $"A moeda do REFINIMP ({valorPrincipal.Moeda}) deve ser igual à moeda do contrato mãe ({contratoPai.Moeda}).");
        }

        // 3. Walk to the original non-REFINIMP ancestor for the BB 70% check
        Contrato ancestral = await repo.GetAncestraNaoRefinimpAsync(req.ContratoMaeId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Não foi possível localizar o ancestral original do contrato mãe '{req.ContratoMaeId}'.");

        Money valorPrincipalAncestral = ancestral.ValorPrincipal;

        // 4. BB-specific: REFINIMP cannot exceed 70% of the original ancestor's principal
        if (banco.CodigoCompe == CodigoCompesBancoDoBrasil)
        {
            Money limiteSetentaPorCento = valorPrincipalAncestral.Multiplicar(0.70m);
            if (valorPrincipal.MaiorQue(limiteSetentaPorCento))
            {
                throw new InvalidOperationException(
                    $"Banco do Brasil (001): o valor do REFINIMP ({valorPrincipal}) excede 70% do principal do contrato ancestral ({limiteSetentaPorCento}).");
            }
        }

        // 5. Calculate percentual: valor do REFINIMP / principal ancestral
        decimal percentualFracao = valorPrincipal.Valor / valorPrincipalAncestral.Valor;
        Percentual percentual = Percentual.DeFracao(percentualFracao);

        // 6. Create and persist the detail
        RefinimpDetail detail = RefinimpDetail.Criar(
            contrato.Id,
            req.ContratoMaeId,
            percentual,
            valorPrincipal,
            clock);

        repo.AddRefinimpDetail(detail);

        // 7. Mark parent status: total if covers 100%, partial otherwise
        if (percentualFracao >= 1.0m)
        {
            contratoPai.MarcarRefinanciadoTotal(clock);
        }
        else
        {
            contratoPai.MarcarRefinanciadoParcial(clock);
        }

        return detail;
    }
}
