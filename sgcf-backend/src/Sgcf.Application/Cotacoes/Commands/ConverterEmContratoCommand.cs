using System.Text.Json;
using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Entity = Sgcf.Domain.Common.Entity;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Inputs específicos da modalidade REFINIMP para o command de conversão.
/// SPEC §4.5, §5.3 — Onda 1.
/// </summary>
/// <param name="PercentualRefinanciado">
/// Percentual declarado de cobertura sobre o mãe imediato (fração 0..1].
/// Serve como auditoria de intenção — o valor armazenado no RefinimpDetail
/// é calculado como valorPrincipal / ancestral.ValorPrincipal (fonte da verdade monetária).
/// </param>
public sealed record RefinimpInputs(decimal PercentualRefinanciado);

/// <summary>
/// Converte cotação aceita em contrato.
/// Cria Contrato + EconomiaNegociacao atomicamente (único SaveChanges no final).
/// Atualiza ValorUtilizadoBRL do LimiteBanco. SPEC §4.1, §5.2.
/// Onda 1: aceita <see cref="RefinimpInputs"/> opcionais para modalidade REFINIMP.
/// </summary>
public sealed record ConverterEmContratoCommand(
    Guid CotacaoId,
    string NumeroExternoContrato,
    string? CodigoInternoContrato,
    DateOnly DataContratacao,
    DateOnly DataVencimento,
    decimal TaxaAa,
    string? Observacoes = null,
    string? RofNumero = null,
    string? ExportadorNome = null,
    string? ExportadorPais = null,
    string? ProdutoImportado = null,
    // Onda 1 REFINIMP — SPEC §5.3: percentual de intenção de cobertura (auditoria).
    // Somente campos REFINIMP aqui — campos de outras modalidades adicionados em ondas paralelas.
    RefinimpInputs? Refinimp = null) : IRequest<ContratoDto>;

public sealed class ConverterEmContratoCommandValidator : AbstractValidator<ConverterEmContratoCommand>
{
    public ConverterEmContratoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
        RuleFor(c => c.NumeroExternoContrato).NotEmpty();
        RuleFor(c => c.TaxaAa).GreaterThan(0m).WithMessage("TaxaAa deve ser maior que zero.");
        RuleFor(c => c.DataVencimento)
            .GreaterThan(c => c.DataContratacao)
            .WithMessage("DataVencimento deve ser posterior a DataContratacao.");
    }
}

public sealed class ConverterEmContratoCommandHandler(
    ICotacaoRepository cotacaoRepo,
    IContratoRepository contratoRepo,
    IEconomiaRepository economiaRepo,
    ILimiteBancoRepository limiteRepo,
    ICdiSnapshotRepository cdiRepo,
    IEnumerable<IConversorModalidade> conversores,
    IClock clock) : IRequestHandler<ConverterEmContratoCommand, ContratoDto>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    // Mapa indexado por modalidade — construído uma vez por instância do handler.
    // Scoped DI garante que o dicionário é recriado por request, compatível com o ciclo dos conversores.
    private readonly IReadOnlyDictionary<ModalidadeContrato, IConversorModalidade> _conversoresMap =
        conversores.ToDictionary(c => c.Modalidade);

    public async Task<ContratoDto> Handle(ConverterEmContratoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await cotacaoRepo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        if (!cotacao.PropostaAceitaId.HasValue)
        {
            throw new InvalidOperationException("Cotação não possui proposta aceita. Aceite uma proposta antes de converter.");
        }

        Proposta propostaAceita = cotacao.Propostas.First(p => p.Id == cotacao.PropostaAceitaId.Value);

        // ── 1. Criar o Contrato ────────────────────────────────────────────────
        LocalDate dataContratacao = new(cmd.DataContratacao.Year, cmd.DataContratacao.Month, cmd.DataContratacao.Day);
        LocalDate dataVencimento = new(cmd.DataVencimento.Year, cmd.DataVencimento.Month, cmd.DataVencimento.Day);

        Money valorPrincipal = propostaAceita.ValorOferecidoMoedaOriginal;
        Percentual taxaAa = Percentual.De(cmd.TaxaAa);

        Contrato contrato = Contrato.Criar(
            numeroExterno: cmd.NumeroExternoContrato,
            bancoId: propostaAceita.BancoId,
            modalidade: cotacao.Modalidade,
            valorPrincipal: valorPrincipal,
            dataContratacao: dataContratacao,
            dataVencimento: dataVencimento,
            taxaAa: taxaAa,
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: propostaAceita.PeriodicidadeJuros,
            estruturaAmortizacao: propostaAceita.EstruturaAmortizacao,
            quantidadeParcelas: 1,
            dataPrimeiroVencimento: dataVencimento,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            periodicidadeJuros: propostaAceita.PeriodicidadeJuros,
            convencaoDataNaoUtil: ConvencaoDataNaoUtil.Following,
            observacoes: cmd.Observacoes);

        string codigoInterno = cmd.CodigoInternoContrato
            ?? await GerarCodigoInternoContratoAsync(contratoRepo, dataContratacao.Year, cancellationToken);

        contrato.SetCodigoInterno(codigoInterno);
        contratoRepo.Add(contrato);

        // Dispatcher: roteia a criação do Detail para o conversor da modalidade registrada.
        // Cada modalidade implementa IConversorModalidade e é registrada em DI.
        // Onda 0 F0.3 — docs/specs/cotacoes/modalidades/onda-0.md §5.5.
        IConversorModalidade conversor = _conversoresMap.GetValueOrDefault(cotacao.Modalidade)
            ?? throw new InvalidOperationException(
                $"Conversor não registrado para modalidade '{cotacao.Modalidade}'. " +
                "Verifique o registro de IConversorModalidade em DependencyInjection.");

        var ctx = new ConverterEmContratoContext(cotacao, propostaAceita, contrato, cmd, clock);
        (Entity detailPrincipal, Entity? detailSecundario) =
            await conversor.CriarDetailAsync(ctx, cancellationToken);

        contratoRepo.AddDetail(detailPrincipal);
        if (detailSecundario is not null)
        {
            contratoRepo.AddDetail(detailSecundario);
        }

        // Cast por modalidade — ContratoDto.From aceita detail específico por modalidade.
        // Novos conversores adicionam o cast correspondente aqui ao serem implementados.
        FinimpDetail? finimpDetail = detailPrincipal as FinimpDetail;
        RefinimpDetail? refinimpDetail = detailPrincipal as RefinimpDetail;

        // ── 2. Calcular CET do contrato fechado ────────────────────────────────
        // Usa a taxa final negociada (cmd.TaxaAa) via override — preserva a proposta original
        // como snapshot imutável e reflete corretamente a economia na transição (SPEC §5.2).
        // Onda 0 F0.2: CalcularCet aceita decimal? — passa PtaxUsadaUsdBrl diretamente.
        // Para FINIMP/REFINIMP/Lei4131: ptax não-null → CalcularCetFinimp.
        // Para modalidades BRL futuras: ptax null → NotImplementedException (Onda futura).
        decimal cetContrato = CalculadoraCet.CalcularCet(
            propostaAceita,
            cotacao.PtaxUsadaUsdBrl,
            dataContratacao,
            taxaAaPercentualOverride: cmd.TaxaAa);

        // ── 3. Criar EconomiaNegociacao ────────────────────────────────────────
        decimal cetProposta = propostaAceita.CetCalculadoAaPercentual
            ?? throw new InvalidOperationException("CET da proposta aceita não calculado. Execute o cálculo antes de converter.");

        string snapshotProposta = JsonSerializer.Serialize(PropostaDto.From(propostaAceita), JsonOpts);
        string snapshotContrato = JsonSerializer.Serialize(new
        {
            contrato.Id,
            contrato.NumeroExterno,
            contrato.CodigoInterno,
            Modalidade = contrato.Modalidade.ToString(),
            ValorPrincipal = valorPrincipal.Valor,
            Moeda = valorPrincipal.Moeda.ToString(),
            DataContratacao = dataContratacao.ToString(),
            DataVencimento = dataVencimento.ToString(),
            TaxaAa = cmd.TaxaAa,
            CetCalculado = cetContrato,
        }, JsonOpts);

        LocalDate hoje = clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;
        CdiSnapshot cdiSnapshot = await cdiRepo.GetMaisRecenteAsync(hoje, cancellationToken)
            ?? throw new InvalidOperationException(
                "Taxa CDI não cadastrada. Cadastre o CDI antes de converter a cotação em contrato.");

        int prazoProposta = propostaAceita.PrazoDias;
        int prazoContrato = Period.Between(dataContratacao, dataVencimento, PeriodUnits.Days).Days;
        // ! null-forgiving seguro: invariante de domínio garante PtaxUsadaUsdBrl não-null
        // para modalidades cambiais (ExigeMoedaEstrangeira). Propostas em moeda não-BRL
        // só existem em cotações FINIMP/REFINIMP/Lei4131 que sempre têm PTAX.
        Money valorPrincipalBrl = propostaAceita.MoedaOriginal == Moeda.Brl
            ? valorPrincipal
            : new Money(Math.Round(valorPrincipal.Valor * cotacao.PtaxUsadaUsdBrl!.Value, 6, MidpointRounding.AwayFromZero), Moeda.Brl);

        (Money economiaBruta, Money economiaAjustada, LocalDate dataRefCdi) = CalculadoraEconomia.Calcular(
            cetProposta,
            cetContrato,
            valorPrincipalBrl,
            prazoProposta,
            prazoContrato,
            cdiSnapshot.CdiAaPercentual,
            cdiSnapshot.Data);

        EconomiaNegociacao economia = EconomiaNegociacao.Criar(
            cotacao.Id,
            contrato.Id,
            snapshotProposta,
            snapshotContrato,
            cetProposta,
            cetContrato,
            economiaBruta,
            economiaAjustada,
            dataRefCdi,
            clock);

        economiaRepo.Add(economia);

        // ── 4. Atualizar LimiteBanco ───────────────────────────────────────────
        LimiteBanco? limite = await limiteRepo.GetByBancoModalidadeAsync(
            propostaAceita.BancoId,
            cotacao.Modalidade,
            cancellationToken);

        if (limite is not null)
        {
            limite.RegistrarUso(valorPrincipalBrl, clock);
            limiteRepo.Update(limite);
        }

        // ── 5. Transição de estado da Cotação ──────────────────────────────────
        cotacao.ConverterEmContrato(contrato.Id, clock);

        // ── 6. Salvar tudo atomicamente (single UoW via SaveChanges) ───────────
        await contratoRepo.SaveChangesAsync(cancellationToken);

        return ContratoDto.From(contrato, finimpDetail, refinimpDetail: refinimpDetail);
    }

    private static async Task<string> GerarCodigoInternoContratoAsync(
        IContratoRepository repo,
        int ano,
        CancellationToken cancellationToken)
    {
        int count = await repo.CountByAnoAsync(ano, cancellationToken);
        return $"FIN-{ano}-{count + 1:D4}";
    }
}
