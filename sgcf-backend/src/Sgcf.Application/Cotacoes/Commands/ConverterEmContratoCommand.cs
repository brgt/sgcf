using System.Text.Json;
using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Exceptions;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using TipoGarantia = Sgcf.Domain.Contratos.TipoGarantia;
using Entity = Sgcf.Domain.Common.Entity;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Garantia declarada pelo operador no ato da conversão cotação→contrato.
/// Usada tanto para o enforcement SC-04 (verificação de cobertura antes da criação)
/// quanto para persistência das garantias no contrato recém-criado.
/// </summary>
/// <param name="Tipo">Nome do <c>TipoGarantia</c> (ex.: "CdbCativo", "Aval").</param>
/// <param name="ValorBrl">Valor da garantia em BRL. Obrigatório para todos os tipos exceto Aval sem valor monetário.</param>
/// <param name="DataConstituicao">Data de constituição da garantia.</param>
/// <param name="Observacoes">Observações opcionais.</param>
public sealed record GarantiaContratoInput(
    string Tipo,
    decimal ValorBrl,
    DateOnly DataConstituicao,
    string? Observacoes = null);

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
/// Inputs específicos da modalidade NCE para o command de conversão.
/// SPEC §6 — Onda 2.
/// </summary>
public sealed record NceInputs(
    string? NceNumero,
    DateOnly? DataEmissao,
    string? BancoMandatario);

/// <summary>
/// Inputs específicos da modalidade Lei 4131/62 para o command de conversão.
/// SPEC §5.3, §6 — Onda 4.
/// </summary>
/// <param name="SblcNumero">Número da SBLC (opcional — operações sem SBLC são "clean Lei 4131").</param>
/// <param name="SblcBancoEmissor">Razão social do banco emissor da SBLC.</param>
/// <param name="SblcValorUsd">Valor de face da SBLC em USD (informativo).</param>
/// <param name="TemMarketFlex">Indica se o contrato possui cláusula de market flex.</param>
/// <param name="BreakFundingFeePercentual">Break funding fee em percentual humano (ex: 1.5 para 1,5%).</param>
/// <param name="PaisCredor">País do credor — ISO 3166-1 alpha-3 (ex: "JPN"). Não persistido no MVP.</param>
/// <param name="AliquotaIrrfPercentual">Alíquota IRRF em percentual humano (ex: 15 para 15%). Informativo.</param>
public sealed record Lei4131Inputs(
    string? SblcNumero,
    string? SblcBancoEmissor,
    decimal? SblcValorUsd,
    bool TemMarketFlex,
    decimal? BreakFundingFeePercentual,
    string? PaisCredor,
    decimal? AliquotaIrrfPercentual);

// Nota: FgiInputs é definido em Sgcf.Domain.Cotacoes — reutilizado diretamente para evitar duplicação.
// A regra de dependência Application→Domain permite este uso (SPEC MD-5).

/// <summary>
/// Inputs específicos da modalidade Capital de Giro para o command de conversão.
/// SPEC §5.3 — Onda 3b.
/// </summary>
/// <param name="NumeroOperacao">
/// Número da operação no sistema interno do banco — opcional (SPEC EC-10).
/// Quando não informado, <see cref="CapitalDeGiroDetail.NumeroOperacao"/> fica null.
/// </param>
public sealed record CapitalDeGiroInputs(string? NumeroOperacao);

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
    RefinimpInputs? Refinimp = null,
    // Onda 2 NCE — SPEC §6: campos específicos de NCE.
    NceInputs? Nce = null,
    // Onda 4 Lei 4131 — SPEC §5.3: campos específicos de Lei 4131/62.
    Lei4131Inputs? Lei4131 = null,
    // Onda 3b Capital de Giro — SPEC §5.3: campos específicos de Capital de Giro.
    CapitalDeGiroInputs? CapitalDeGiro = null,
    // Onda 3a FGI — SPEC fgi.md §6.1: número de operação (opcional) separado dos inputs financeiros.
    string? NumeroOperacaoFgi = null,
    // Onda 3a FGI — SPEC fgi.md §6.1: taxa e percentual de cobertura do FGI.
    FgiInputs? Fgi = null,
    // S34 Fase 3 — SC-04: garantias declaradas pelo operador no ato da conversão.
    // Usadas para enforcement de cobertura e persistência no contrato criado.
    // Null ou lista vazia = nenhuma garantia declarada (válido quando não há política obrigatória).
    IReadOnlyList<GarantiaContratoInput>? GarantiasContrato = null) : IRequest<ContratoDto>;

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

        RuleForEach(c => c.GarantiasContrato)
            .ChildRules(g =>
            {
                g.RuleFor(i => i.Tipo)
                    .NotEmpty()
                    .Must(t => Enum.TryParse<TipoGarantia>(t, ignoreCase: true, out _))
                    .WithMessage("TipoGarantia inválido.");
                g.RuleFor(i => i.ValorBrl)
                    .GreaterThanOrEqualTo(0m)
                    .WithMessage("ValorBrl da garantia não pode ser negativo.");
            })
            .When(c => c.GarantiasContrato is { Count: > 0 });
    }
}

public sealed class ConverterEmContratoCommandHandler(
    ICotacaoRepository cotacaoRepo,
    IContratoRepository contratoRepo,
    IEconomiaRepository economiaRepo,
    ILimiteBancoRepository limiteRepo,
    ILimiteGlobalBancoRepository limiteGlobalRepo,
    ICdiSnapshotRepository cdiRepo,
    IGarantiaRepository garantiaRepo,
    IBancoRepository bancoRepo,
    IConsultaSaldoBanco saldo,
    ITenantContext tenantContext,
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

        // ── 1. Preparar dados financeiros ──────────────────────────────────────
        LocalDate dataContratacao = new(cmd.DataContratacao.Year, cmd.DataContratacao.Month, cmd.DataContratacao.Day);
        LocalDate dataVencimento = new(cmd.DataVencimento.Year, cmd.DataVencimento.Month, cmd.DataVencimento.Day);

        Money valorPrincipal = propostaAceita.ValorOferecidoMoedaOriginal;
        Percentual taxaAa = Percentual.De(cmd.TaxaAa);

        // Converte principal para BRL (necessário para enforcement SC-04 e cálculo de economia).
        // ! null-forgiving seguro: invariante de domínio garante PtaxUsadaUsdBrl não-null
        // para modalidades cambiais (ExigeMoedaEstrangeira). Propostas em moeda não-BRL
        // só existem em cotações FINIMP/REFINIMP/Lei4131 que sempre têm PTAX.
        Money valorPrincipalBrl = propostaAceita.MoedaOriginal == Moeda.Brl
            ? valorPrincipal
            : new Money(Math.Round(valorPrincipal.Valor * cotacao.PtaxUsadaUsdBrl!.Value, 6, MidpointRounding.AwayFromZero), Moeda.Brl);

        // ── 1b. Lookup de política do banco (SC-01..SC-03) ─────────────────────
        // Feito ANTES de Contrato.Criar para que enforcement SC-04 possa rodar sem contrato.
        // Lookup temporal: retorna o LimiteBanco cujo período [DataVigenciaInicio, DataVigenciaFim]
        // contém dataContratacao. Null se banco não tiver limite cadastrado (SC-07).
        LimiteBanco? limiteBancoVigente = await limiteRepo.GetVigenteByBancoModalidadeAsync(
            bancoId: propostaAceita.BancoId,
            modalidade: cotacao.Modalidade,
            dataReferencia: dataContratacao,
            cancellationToken);

        // SC-02: LimiteGlobalBanco vigente para o banco na data de contratação.
        LimiteGlobalBanco? limiteGlobalVigente = await limiteGlobalRepo.GetVigenteByBancoAsync(
            bancoId: propostaAceita.BancoId,
            hoje: dataContratacao,
            ct: cancellationToken);

        // SC-04: Enforcement — bloqueia conversão se garantias obrigatórias não estão cobertas.
        // Roda ANTES de Contrato.Criar para que uma falha não persista estado parcial.
        // SC-07: sem LimiteBanco ou sem revisão vigente → enforcement desligado.
        // Captura a política vigente NA data de contratação.
        // Usa início do dia seguinte como limite exclusivo para incluir revisões criadas
        // a qualquer hora em dataContratacao (ex.: limite criado às 10h, contrato às 15h).
        Instant fimExclusivoContratacao = dataContratacao
            .PlusDays(1)
            .AtStartOfDayInZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"])
            .ToInstant();
        GarantiaExigidaRevisao? revisaoVigente = limiteBancoVigente?.RevisaoVigenteEm(fimExclusivoContratacao);
        if (revisaoVigente is not null)
        {
            var itensObrigatorios = revisaoVigente.Itens.Where(i => i.Obrigatoria).ToList();
            List<LacunaGarantia> lacunas = AvaliarCobertura(
                itensObrigatorios,
                cmd.GarantiasContrato ?? [],
                valorPrincipalBrl);

            if (lacunas.Count > 0)
            {
                throw new GarantiaExigidaNaoCobertaException(
                    limiteBancoId: limiteBancoVigente!.Id,
                    garantiasExigidasRevisaoId: revisaoVigente.Id,
                    lacunas: lacunas);
            }
        }

        // ── 1c. Enforcement de teto por regime (LG-11 / LG-12) ─────────────────
        // SPEC_REGIME_LIMITE_EXPLICITO §4.4. Roda ANTES de Contrato.Criar para não
        // persistir estado parcial. Banco inexistente (apenas em testes mockados) → pulado.
        // O Banco é carregado para uso do Apelido nas mensagens; a detecção de regime usa
        // IConsultaSaldoBanco.BancoEmRegimePerModalityAsync (fonte única — mesma usada pelos
        // handlers de limite global e por AdicionarBancoNaCotacao).
        Banco? banco = await bancoRepo.GetByIdAsync(propostaAceita.BancoId, cancellationToken);
        if (banco is not null)
        {
            Guid tenantId = tenantContext.TenantId;
            bool perModalidade = await saldo.BancoEmRegimePerModalityAsync(
                propostaAceita.BancoId, tenantId, cancellationToken);

            if (!perModalidade)
            {
                // LG-12: consumo do teto global. O global é calculado dinamicamente a partir
                // dos contratos ativos; criar o contrato já consome — não há RegistrarUso.
                if (limiteGlobalVigente is null)
                {
                    throw new InvalidOperationException(
                        $"Banco '{banco.Apelido}' opera em regime de limite global, " +
                        "mas não possui limite global vigente cadastrado. [REG-03]");
                }

                Money saldoDevedor = await saldo.CalcularSaldoDevedorBancoAsync(
                    propostaAceita.BancoId, tenantId, cancellationToken);

                if (saldoDevedor.Valor + valorPrincipalBrl.Valor > limiteGlobalVigente.ValorLimiteBrl.Valor)
                {
                    throw new InvalidOperationException(
                        $"Contratação excede o limite global do banco '{banco.Apelido}'. " +
                        $"Saldo devedor: BRL {saldoDevedor.Valor:F2}, principal: BRL {valorPrincipalBrl.Valor:F2}, " +
                        $"limite: BRL {limiteGlobalVigente.ValorLimiteBrl.Valor:F2}. [LG-12]");
                }
            }
            else
            {
                // PerModalidade — LG-11: exige LimiteBanco na modalidade e respeita o teto global agregado.
                if (limiteBancoVigente is null)
                {
                    throw new InvalidOperationException(
                        $"Modalidade '{cotacao.Modalidade}' requer LimiteBanco registrado neste banco " +
                        "— regime per-modalidade. [LG-11]");
                }

                if (limiteBancoVigente.ValorDisponivelBrl.Valor < valorPrincipalBrl.Valor)
                {
                    throw new InvalidOperationException(
                        $"Banco '{banco.Apelido}' não possui limite disponível suficiente na modalidade " +
                        $"'{cotacao.Modalidade}'. Disponível: BRL {limiteBancoVigente.ValorDisponivelBrl.Valor:F2}, " +
                        $"necessário: BRL {valorPrincipalBrl.Valor:F2}. [LG-11]");
                }

                if (limiteGlobalVigente is not null)
                {
                    Money utilizadoAgregado = await saldo.CalcularUtilizadoAgregadoModalidadesAsync(
                        propostaAceita.BancoId, tenantId, cancellationToken);

                    if (utilizadoAgregado.Valor + valorPrincipalBrl.Valor > limiteGlobalVigente.ValorLimiteBrl.Valor)
                    {
                        throw new InvalidOperationException(
                            $"Contratação excede o teto global agregado do banco '{banco.Apelido}'. " +
                            $"Utilizado: BRL {utilizadoAgregado.Valor:F2}, principal: BRL {valorPrincipalBrl.Valor:F2}, " +
                            $"limite global: BRL {limiteGlobalVigente.ValorLimiteBrl.Valor:F2}. [LG-11]");
                    }
                }
            }
        }

        // ── 2. Criar o Contrato ────────────────────────────────────────────────
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

        // ── 2b. Vincular política do banco (SC-01..SC-03) ─────────────────────
        // Os lookups já foram feitos antes do enforcement (passo 1b acima).
        // Reutiliza as variáveis já resolvidas: limiteBancoVigente, limiteGlobalVigente, revisaoVigente.
        contrato.VincularPoliticaBanco(
            limiteBancoId: limiteBancoVigente?.Id,
            limiteGlobalBancoId: limiteGlobalVigente?.Id,
            garantiasExigidasRevisaoId: revisaoVigente?.Id);

        // ── 2c. Persistir garantias declaradas no command (SC-04 já validado acima) ──
        // Cada GarantiaContratoInput se torna uma entidade Garantia no contrato recém-criado.
        // O SaveChanges ao final inclui essas garantias atomicamente.
        if (cmd.GarantiasContrato is { Count: > 0 })
        {
            decimal? principalBrlParaCalculo = contrato.Moeda == Moeda.Brl
                ? contrato.ValorPrincipal.Valor
                : null;

            foreach (GarantiaContratoInput garantiaInput in cmd.GarantiasContrato)
            {
                if (!Enum.TryParse<TipoGarantia>(garantiaInput.Tipo, ignoreCase: true, out TipoGarantia tipoGarantia))
                {
                    throw new ArgumentException(
                        $"TipoGarantia inválido na garantia declarada: '{garantiaInput.Tipo}'.");
                }

                LocalDate dataConst = new(
                    garantiaInput.DataConstituicao.Year,
                    garantiaInput.DataConstituicao.Month,
                    garantiaInput.DataConstituicao.Day);

                Garantia garantia = Garantia.Criar(
                    contratoId: contrato.Id,
                    tipo: tipoGarantia,
                    valorBrl: new Money(garantiaInput.ValorBrl, Moeda.Brl),
                    principalBrlParaCalculo: principalBrlParaCalculo,
                    dataConstituicao: dataConst,
                    dataLiberacaoPrevista: null,
                    observacoes: garantiaInput.Observacoes,
                    createdBy: "conversor-cotacao",
                    clock: clock);

                garantiaRepo.Add(garantia);
            }
        }

        // ── 3. Dispatcher de Detail por modalidade ─────────────────────────────
        // Roteia a criação do Detail para o conversor da modalidade registrada.
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
        Lei4131Detail? lei4131Detail = detailPrincipal as Lei4131Detail;                     // Onda 4
        NceDetail? nceDetail = detailPrincipal as NceDetail;                                 // Onda 2
        CapitalDeGiroDetail? capitalDeGiroDetail = detailPrincipal as CapitalDeGiroDetail;   // Onda 3b
        FgiDetail? fgiDetail = detailPrincipal as FgiDetail;                                 // Onda 3a

        // ── 4. Calcular CET do contrato fechado ────────────────────────────────
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

        // ── 5. Criar EconomiaNegociacao ────────────────────────────────────────
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
        // valorPrincipalBrl foi calculado no passo 1 (antes do enforcement SC-04) e reutilizado aqui.

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

        // ── 6. Atualizar LimiteBanco ───────────────────────────────────────────
        // Usa GetByIdTracking com o Id já resolvido no passo 1b para garantir que é o mesmo
        // agregado — evita race condition entre GetVigenteByBancoModalidade e GetByBancoModalidade.
        if (limiteBancoVigente is not null)
        {
            LimiteBanco? limiteTracked = await limiteRepo.GetByIdTrackingAsync(
                limiteBancoVigente.Id, cancellationToken);
            if (limiteTracked is not null)
            {
                limiteTracked.RegistrarUso(valorPrincipalBrl, clock);
                limiteRepo.Update(limiteTracked);
            }
        }

        // ── 7. Transição de estado da Cotação ──────────────────────────────────
        cotacao.ConverterEmContrato(contrato.Id, clock);

        // ── 8. Salvar tudo atomicamente (single UoW via SaveChanges) ───────────
        // Inclui: Contrato, Detail, Garantias declaradas, EconomiaNegociacao, Cotacao.
        await contratoRepo.SaveChangesAsync(cancellationToken);

        return ContratoDto.From(
            contrato,
            finimpDetail,
            lei4131Detail: lei4131Detail,
            refinimpDetail: refinimpDetail,
            nceDetail: nceDetail,
            capitalDeGiroDetail: capitalDeGiroDetail,
            fgiDetail: fgiDetail);
    }

    /// <summary>
    /// Avalia se cada item obrigatório da revisão vigente está coberto pelas garantias
    /// declaradas no command. Retorna lista de lacunas (vazia = cobertura completa).
    ///
    /// Regras por tipo de item (SPEC §4.4):
    /// - <c>PercentualSobreLimite</c>: valor esperado = percentual/100 × valorPrincipalBrl.
    /// - <c>ValorFixoBrl</c>: valor esperado = ValorFixoBrl.
    /// - Aval sem percentual e sem valor fixo: cobertura satisfeita pela presença de qualquer
    ///   garantia do tipo Aval no contrato, independente do valor.
    ///
    /// Usa <see cref="CalculadorValorGarantiaExigida"/> para calcular o valor esperado,
    /// garantindo consistência com a feature de preenchimento automático de cotações.
    /// </summary>
    private static List<LacunaGarantia> AvaliarCobertura(
        IReadOnlyList<GarantiaExigidaItem> itensObrigatorios,
        IReadOnlyList<GarantiaContratoInput> garantiasDeclaradas,
        Money valorPrincipalBrl)
    {
        // Agrupa garantias declaradas por tipo para somar valor coberto por tipo.
        Dictionary<TipoGarantia, decimal> valorCobertoPorTipo =
            garantiasDeclaradas
                .Where(g => Enum.TryParse<TipoGarantia>(g.Tipo, ignoreCase: true, out _))
                .GroupBy(
                    g => Enum.Parse<TipoGarantia>(g.Tipo, ignoreCase: true),
                    g => g.ValorBrl)
                .ToDictionary(grp => grp.Key, grp => grp.Sum());

        // Delega a avaliação (itens independentes + grupos "OU" por fração) ao avaliador puro.
        return AvaliadorCoberturaGarantia.Avaliar(itensObrigatorios, valorCobertoPorTipo, valorPrincipalBrl);
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
