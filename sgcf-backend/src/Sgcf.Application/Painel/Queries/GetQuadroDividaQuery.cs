using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Cache;
using Sgcf.Application.Sistema;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Painel;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Retorna o quadro da dívida para o ano informado: snapshot atual + projeção mês a mês + sumário anual.
///
/// Decisões de design:
///   AD-7  — endpoint único que combina saldo inicial, projeção e sumário.
///   AD-9  — sem cenarioId retorna apenas dados reais (contratos ativos + amortizações futuras).
///   Q9    — apenas o ano corrente é suportado no MVP. Anos passados/futuros lançam
///            <see cref="InvalidOperationException"/> com mensagem contendo "MVP".
///            A Fase 2 implementará snapshot histórico e projeção de anos futuros.
/// </summary>
/// <param name="Ano">Ano civil a consultar. Deve estar entre 2020 e 2050 inclusive.</param>
/// <param name="CenarioId">
/// Identificador opcional de um cenário de simulação (Fase 3 Task 3.1).
/// Quando presente, a projeção incorpora as captações hipotéticas do cenário.
/// Null retorna apenas dados reais (AD-9).
/// </param>
public sealed record GetQuadroDividaQuery(int Ano, Guid? CenarioId = null) : IRequest<QuadroDividaDto>;

/// <summary>
/// Handler do <see cref="GetQuadroDividaQuery"/>.
///
/// Orquestra:
///   1. Saldo inicial via <see cref="GetSaldoPorBancoAtualQuery"/> (reutiliza Task 1.2).
///   2. Contratos ativos para montar o mapa <c>ContratoId → BancoId</c>.
///   3. Eventos de amortização de principal do cronograma para o ano.
///   4. Conversão para <see cref="EventoProjecao"/> com taxa BRL corrente.
///   5. (Fase 3) Se cenarioId informado: carrega cenário, valida, gera eventos de captação
///      e amortização hipotéticos via <see cref="SimulacaoCronogramaCalculator"/>.
///   6. Invocação do <see cref="ProjetorSaldoMensal"/> (função pura AD-5).
///   7. Mapeamento para DTOs com <c>bancoApelido</c> (AD-10).
///   8. Cálculo do sumário anual.
///
/// Nota sobre <c>IContratoRepository</c>: o campo <c>EventoCronograma</c> não guarda
/// <c>BancoId</c> diretamente — o vínculo é via <c>ContratoId</c>. O repositório de contratos
/// é necessário para construir o mapa <c>ContratoId → BancoId</c>.
///
/// Nota sobre CDI (Fase 3): simulações com TipoTaxa = CdiSpread requerem snapshot de CDI.
/// Se não houver snapshot disponível, lança <see cref="InvalidOperationException"/> com mensagem clara.
///
/// Nota sobre cache (<see cref="ICronogramaSimulacaoCache"/>): sempre injetado via DI — em ambientes
/// sem Redis, <c>NullCronogramaSimulacaoCache</c> é registrado como fallback explícito (AD-3).
/// </summary>
public sealed class GetQuadroDividaQueryHandler(
    IMediator mediator,
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    IBancoRepository bancoRepo,
    ICotacaoSpotCache spotCache,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock,
    ICenarioSimulacaoRepository cenarioRepo,
    ICronogramaSimulacaoCache cronogramaCache,
    ICdiSnapshotRepository cdiRepo,
    IParametroSistemaRepository parametroSistemaRepo) : IRequestHandler<GetQuadroDividaQuery, QuadroDividaDto>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    /// <inheritdoc />
    public async Task<QuadroDividaDto> Handle(
        GetQuadroDividaQuery request,
        CancellationToken cancellationToken)
    {
        ValidarAno(request.Ano);
        ValidarAnoCorrenteMvp(request.Ano, clock.GetCurrentInstant().InZone(FusoBrasilia).Year);

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        SaldoPorBancoAtualDto snapshot = await CarregarSnapshotAsync(cancellationToken);
        (List<Contrato> ativos, Dictionary<Guid, Guid> bancoIdPorContrato, Dictionary<Guid, Moeda> moedaPorContrato)
            = await CarregarContratosAtivosAsync(cancellationToken);

        Dictionary<Guid, Money> saldoInicialPorBanco = ExtrairSaldoInicialPorBanco(snapshot);
        List<EventoProjecao> eventosProjecao = await CarregarEventosProjecaoAsync(
            request.Ano, ativos, bancoIdPorContrato, moedaPorContrato, hoje, cancellationToken);

        CenarioAplicadoDto? cenarioAplicadoDto = await AplicarCenarioSeInformadoAsync(
            request, hoje, eventosProjecao, cancellationToken);

        QuadroDividaProjecao projecao = ProjetorSaldoMensal.Projetar(
            saldoInicialPorBanco, eventosProjecao, request.Ano);

        Dictionary<Guid, string> apelidos = await CarregarApelidosBancosAsync(cancellationToken);
        QuadroDividaProjecaoDto projecaoDto = MapearProjecaoDto(projecao, apelidos);
        QuadroDividaSumarioDto sumario = CalcularSumario(projecao);

        IReadOnlyList<string> alertasTetao = await CalcularAlertasTetaoAsync(projecaoDto, cancellationToken);

        DateOnly dataReferencia = new(snapshot.DataReferencia.Year, snapshot.DataReferencia.Month, snapshot.DataReferencia.Day);

        return new QuadroDividaDto(
            Ano: request.Ano,
            DataReferencia: dataReferencia,
            SnapshotInicial: snapshot,
            Projecao: projecaoDto,
            Sumario: sumario,
            Alertas: alertasTetao,
            CenarioAplicado: cenarioAplicadoDto);
    }

    // ── Orquestração — passos extraídos do Handle ──────────────────────────────

    /// <summary>Obtém o snapshot atual via GetSaldoPorBancoAtualQuery (Task 1.2).</summary>
    private Task<SaldoPorBancoAtualDto> CarregarSnapshotAsync(CancellationToken ct) =>
        mediator.Send(new GetSaldoPorBancoAtualQuery(), ct);

    /// <summary>
    /// Carrega contratos ativos e constrói os mapas de lookup ContratoId→BancoId e ContratoId→Moeda.
    /// EventoCronograma vincula-se por ContratoId, não por BancoId — estes mapas são necessários.
    /// </summary>
    private async Task<(List<Contrato> Ativos, Dictionary<Guid, Guid> BancoIdPorContrato, Dictionary<Guid, Moeda> MoedaPorContrato)>
        CarregarContratosAtivosAsync(CancellationToken ct)
    {
        IReadOnlyList<Contrato> todos = await contratoRepo.ListAsync(ct);
        List<Contrato> ativos = todos.Where(c => c.Status == StatusContrato.Ativo).ToList();
        return (
            ativos,
            ativos.ToDictionary(c => c.Id, c => c.BancoId),
            ativos.ToDictionary(c => c.Id, c => c.Moeda));
    }

    /// <summary>Converte o snapshot em BancoId → Money(BRL). Garante consistência com PainelDivida (AD-8).</summary>
    private static Dictionary<Guid, Money> ExtrairSaldoInicialPorBanco(SaldoPorBancoAtualDto snapshot) =>
        snapshot.Bancos.ToDictionary(b => b.BancoId, b => new Money(b.SaldoBrl, Moeda.Brl));

    /// <summary>
    /// Carrega eventos do cronograma e os converte em <see cref="EventoProjecao"/>,
    /// resolvendo cotações spot → PTAX D-1 para moedas estrangeiras.
    /// </summary>
    private async Task<List<EventoProjecao>> CarregarEventosProjecaoAsync(
        int ano,
        List<Contrato> ativos,
        Dictionary<Guid, Guid> bancoIdPorContrato,
        Dictionary<Guid, Moeda> moedaPorContrato,
        LocalDate hoje,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> contratoIds = ativos.Select(c => c.Id).ToList().AsReadOnly();
        IReadOnlyList<EventoCronograma> eventosDb =
            await cronogramaRepo.ListAbertosParaAnoAsync(ano, contratoIds, ct);

        IReadOnlySet<Moeda> moedasEstrangeiras = ativos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxas = await ResolverCotacoesAsync(moedasEstrangeiras, hoje, ct);

        return ConverterParaEventosProjecao(eventosDb, bancoIdPorContrato, moedaPorContrato, taxas);
    }

    /// <summary>
    /// Quando <see cref="GetQuadroDividaQuery.CenarioId"/> está presente, carrega e valida o cenário,
    /// gera eventos hipotéticos e os adiciona a <paramref name="eventosProjecao"/> in-place.
    /// Retorna o DTO de metadados do cenário, ou null quando não há cenário (AD-9).
    /// </summary>
    private async Task<CenarioAplicadoDto?> AplicarCenarioSeInformadoAsync(
        GetQuadroDividaQuery request,
        LocalDate hoje,
        List<EventoProjecao> eventosProjecao,
        CancellationToken ct)
    {
        if (!request.CenarioId.HasValue)
        {
            return null;
        }

        CenarioSimulacao cenario = await cenarioRepo.GetByIdAsync(request.CenarioId.Value, ct)
            ?? throw new KeyNotFoundException(
                $"Cenário de simulação '{request.CenarioId.Value}' não encontrado.");

        ValidarAnoBaseCenario(cenario, request.Ano);

        // CDI lazy: carregado no máximo uma vez por chamada, somente se alguma simulação usar CdiSpread.
        var cdiLoader = new CdiLoader(cdiRepo, hoje);

        List<EventoProjecao> eventosCenario = await GerarEventosDoCenarioAsync(
            cenario, request.Ano, () => cdiLoader.ObterAsync(ct), ct);

        eventosProjecao.AddRange(eventosCenario);

        return new CenarioAplicadoDto(
            Id: cenario.Id,
            Nome: cenario.Nome,
            Status: cenario.Status.ToString(),
            AnoBase: cenario.AnoBase,
            QuantidadeSimulacoes: cenario.Simulacoes.Count);
    }

    /// <summary>Carrega apelidos de todos os bancos para enriquecer os DTOs (AD-10).</summary>
    private async Task<Dictionary<Guid, string>> CarregarApelidosBancosAsync(CancellationToken ct)
    {
        IReadOnlyList<Banco> bancos = await bancoRepo.ListAllAsync(ct);
        return bancos.ToDictionary(b => b.Id, b => b.Apelido);
    }

    /// <summary>
    /// Carrega o parâmetro de tetão e executa a validação pura — nenhuma I/O adicional
    /// além da leitura do parâmetro (Task 3.4 — D-11).
    /// Quando o tenant não tem ParametroSistema (não provisionado), a validação é ignorada.
    /// </summary>
    private async Task<IReadOnlyList<string>> CalcularAlertasTetaoAsync(
        QuadroDividaProjecaoDto projecaoDto,
        CancellationToken ct)
    {
        Domain.Sistema.ParametroSistema? parametros =
            await parametroSistemaRepo.GetAsync(ct);
        return ValidadorTetaoMensal.Validar(projecaoDto, parametros?.TetaoMensalCapacidadeBrl?.Valor);
    }

    // ── Cenário de simulação (Fase 3 Task 3.1) ────────────────────────────────

    /// <summary>
    /// Valida que o AnoBase do cenário é compatível com o ano consultado.
    /// Lança <see cref="InvalidOperationException"/> caso contrário.
    /// O controller mapeia essa exceção para 409 Conflict.
    /// </summary>
    private static void ValidarAnoBaseCenario(CenarioSimulacao cenario, int anoConsultado)
    {
        if (cenario.AnoBase != anoConsultado)
        {
            throw new InvalidOperationException(
                $"O cenário '{cenario.Nome}' tem AnoBase={cenario.AnoBase}, " +
                $"mas a requisição é para o ano {anoConsultado}. " +
                "Crie um cenário com o AnoBase correto ou ajuste o parâmetro 'ano'.");
        }
    }

    /// <summary>
    /// Gera eventos de projeção (Captacao + AmortizacaoPrincipal) para cada simulação do cenário.
    ///
    /// Cada simulação contribui com:
    ///   - Um evento <see cref="TipoEventoProjecao.Captacao"/> na <c>DataContratacaoPrevista</c>.
    ///   - Eventos <see cref="TipoEventoProjecao.AmortizacaoPrincipal"/> a partir do cronograma
    ///     calculado pelo <see cref="SimulacaoCronogramaCalculator"/>. Eventos fora do ano
    ///     são incluídos aqui — o projetor os filtrará (AD-5 / P-5).
    ///
    /// O cache (<see cref="ICronogramaSimulacaoCache"/>) é sempre injetado — em ambientes sem Redis
    /// o <c>NullCronogramaSimulacaoCache</c> é o fallback, que recalcula on-the-fly (AD-3).
    /// </summary>
    private async Task<List<EventoProjecao>> GerarEventosDoCenarioAsync(
        CenarioSimulacao cenario,
        int ano,
        Func<Task<decimal?>> obterCdi,
        CancellationToken ct)
    {
        List<EventoProjecao> eventos = new();

        foreach (SimulacaoContratacao simulacao in cenario.Simulacoes)
        {
            // Evento de captação na data de contratação prevista (AD-6)
            eventos.Add(new EventoProjecao(
                BancoId: simulacao.BancoId,
                Data: simulacao.DataContratacaoPrevista,
                Tipo: TipoEventoProjecao.Captacao,
                ValorBrl: new Money(simulacao.ValorPrincipal.Valor, Moeda.Brl)));

            // Resolve CDI se necessário (lazy: apenas quando TipoTaxa = CdiSpread)
            decimal? cdiAa = simulacao.TipoTaxa == TipoTaxa.CdiSpread
                ? await obterCdi()
                : null;

            // Cronograma hipotético via cache injetado (AD-3).
            // NullCronogramaSimulacaoCache é o fallback quando Redis não está configurado.
            IReadOnlyList<EventoCronogramaGerado> cronograma = await cronogramaCache.GetOrCreateAsync(
                cenario.Id,
                simulacao.Id,
                simulacao.Version,
                () => Task.FromResult(SimulacaoCronogramaCalculator.Calcular(simulacao, cdiAa)),
                ct);

            // Converte apenas eventos de Principal em AmortizacaoPrincipal (juros não entram — AD-6)
            foreach (EventoCronogramaGerado evento in cronograma)
            {
                if (evento.Tipo != TipoEventoCronograma.Principal)
                {
                    continue;
                }

                eventos.Add(new EventoProjecao(
                    BancoId: simulacao.BancoId,
                    Data: evento.DataPrevista,
                    Tipo: TipoEventoProjecao.AmortizacaoPrincipal,
                    ValorBrl: new Money(evento.Valor.Valor, Moeda.Brl)));
            }
        }

        return eventos;
    }

    // ── Validações ─────────────────────────────────────────────────────────────

    /// <summary>Valida o intervalo de ano aceito (2020–2050). Guarda rápida no topo do handler.</summary>
    private static void ValidarAno(int ano)
    {
        if (ano < 2020 || ano > 2050)
        {
            throw new ArgumentException(
                $"Ano deve estar entre 2020 e 2050. Valor recebido: {ano}.",
                nameof(ano));
        }
    }

    /// <summary>
    /// Restrição MVP Q9: apenas o ano corrente é suportado.
    /// Anos passados/futuros requerem snapshot histórico (Fase 2).
    /// Lança <see cref="InvalidOperationException"/> que o controller mapeia para 409.
    /// </summary>
    private static void ValidarAnoCorrenteMvp(int anoConsultado, int anoCorrente)
    {
        if (anoConsultado != anoCorrente)
        {
            throw new InvalidOperationException(
                $"No MVP apenas o ano corrente ({anoCorrente}) é suportado. " +
                $"Projeção de anos passados/futuros sem snapshot histórico não está implementada. " +
                "Use o ano corrente ou aguarde a Fase 2 (MVP constraint Q9).");
        }
    }

    // ── Conversão de eventos ───────────────────────────────────────────────────

    /// <summary>
    /// Converte <see cref="EventoCronograma"/> do tipo <see cref="TipoEventoCronograma.Principal"/>
    /// em <see cref="EventoProjecao"/>. Eventos de outros tipos são silenciosamente ignorados —
    /// a projeção de saldo trata apenas amortizações de principal.
    /// </summary>
    private static List<EventoProjecao> ConverterParaEventosProjecao(
        IReadOnlyList<EventoCronograma> eventos,
        Dictionary<Guid, Guid> bancoIdPorContrato,
        Dictionary<Guid, Moeda> moedaPorContrato,
        Dictionary<Moeda, decimal> taxas)
    {
        var resultado = new List<EventoProjecao>(eventos.Count);

        foreach (EventoCronograma evento in eventos)
        {
            if (evento.Tipo != TipoEventoCronograma.Principal)
            {
                continue;
            }

            if (!bancoIdPorContrato.TryGetValue(evento.ContratoId, out Guid bancoId))
            {
                // Contrato deletado logicamente sem encerrar o cronograma.
                // Ignoramos silenciosamente — o saldo deste contrato já não aparece no snapshot.
                continue;
            }

            Moeda moeda = moedaPorContrato.TryGetValue(evento.ContratoId, out Moeda m) ? m : evento.Moeda;
            decimal taxa = moeda == Moeda.Brl
                ? 1m
                : taxas.GetValueOrDefault(moeda, defaultValue: 0m);

            decimal valorBrl = Math.Round(
                evento.ValorMoedaOriginal.Valor * taxa,
                6,
                MidpointRounding.AwayFromZero);

            resultado.Add(new EventoProjecao(
                BancoId: bancoId,
                Data: evento.DataPrevista,
                Tipo: TipoEventoProjecao.AmortizacaoPrincipal,
                ValorBrl: new Money(valorBrl, Moeda.Brl)));
        }

        return resultado;
    }

    // ── Mapeamento para DTOs ───────────────────────────────────────────────────

    private static QuadroDividaProjecaoDto MapearProjecaoDto(
        QuadroDividaProjecao projecao,
        Dictionary<Guid, string> apelidos)
    {
        List<MesProjecaoDto> meses = new(12);

        foreach (MesProjecao mes in projecao.Meses)
        {
            List<SaldoBancoMesDto> bancosDto = mes.SaldosPorBanco
                .Select(b => new SaldoBancoMesDto(
                    BancoId: b.BancoId,
                    BancoApelido: apelidos.TryGetValue(b.BancoId, out string? apelido)
                        ? apelido
                        : b.BancoId.ToString("N"),
                    SaldoInicio: DecimalArredondamento.Mostrar(b.SaldoInicio.Valor),
                    SaldoFim: DecimalArredondamento.Mostrar(b.SaldoFim.Valor),
                    TotalAmortizacaoNoMes: DecimalArredondamento.Mostrar(b.TotalAmortizacaoNoMes.Valor),
                    TotalCaptacaoNoMes: DecimalArredondamento.Mostrar(b.TotalCaptacaoNoMes.Valor),
                    SharePercentual: DecimalArredondamento.Mostrar(b.SharePercentual)))
                .ToList();

            decimal totalAmortizacaoMes = bancosDto.Sum(b => b.TotalAmortizacaoNoMes);
            decimal totalCaptacaoMes = bancosDto.Sum(b => b.TotalCaptacaoNoMes);

            meses.Add(new MesProjecaoDto(
                Ano: mes.AnoCalendar,
                Mes: mes.Mes,
                Bancos: bancosDto.AsReadOnly(),
                SaldoTotalInicio: DecimalArredondamento.Mostrar(mes.SaldoTotalInicio.Valor),
                SaldoTotalFim: DecimalArredondamento.Mostrar(mes.SaldoTotalFim.Valor),
                TotalAmortizacaoMes: DecimalArredondamento.Mostrar(totalAmortizacaoMes),
                TotalCaptacaoMes: DecimalArredondamento.Mostrar(totalCaptacaoMes)));
        }

        return new QuadroDividaProjecaoDto(meses.AsReadOnly());
    }

    /// <summary>
    /// Calcula os totais anuais agregados a partir dos 12 meses projetados.
    /// </summary>
    private static QuadroDividaSumarioDto CalcularSumario(QuadroDividaProjecao projecao)
    {
        decimal saldoInicioAno = projecao.Meses[0].SaldoTotalInicio.Valor;
        decimal saldoFimAno = projecao.Meses[11].SaldoTotalFim.Valor;

        // Soma todas as amortizações e captações dos 12 meses por banco
        decimal totalAmortizacao = projecao.Meses
            .SelectMany(m => m.SaldosPorBanco)
            .Sum(b => b.TotalAmortizacaoNoMes.Valor);

        decimal totalCaptacao = projecao.Meses
            .SelectMany(m => m.SaldosPorBanco)
            .Sum(b => b.TotalCaptacaoNoMes.Valor);

        decimal variacao = saldoInicioAno == 0m
            ? 0m
            : Math.Round(
                (saldoFimAno - saldoInicioAno) / saldoInicioAno * 100m,
                4,
                MidpointRounding.AwayFromZero);

        return new QuadroDividaSumarioDto(
            SaldoTotalInicioAno: DecimalArredondamento.Mostrar(saldoInicioAno),
            SaldoTotalFimAno: DecimalArredondamento.Mostrar(saldoFimAno),
            TotalAmortizacaoNoAno: DecimalArredondamento.Mostrar(totalAmortizacao),
            TotalCaptacaoNoAno: DecimalArredondamento.Mostrar(totalCaptacao),
            VariacaoAnualPercentual: DecimalArredondamento.Mostrar(variacao));
    }

    // ── Resolução de cotações (estratégia idêntica ao GetSaldoPorBancoAtualQueryHandler) ──

    /// <summary>
    /// Spot intraday Redis → PTAX D-1 mid-rate → zero (moeda contribui com zero BRL).
    /// Mesma estratégia do GetSaldoPorBancoAtualQueryHandler para garantir consistência.
    /// </summary>
    private async Task<Dictionary<Moeda, decimal>> ResolverCotacoesAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate hoje,
        CancellationToken ct)
    {
        Dictionary<Moeda, decimal> resultado = new(capacity: moedas.Count);

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, ct);

            if (spot is not null)
            {
                resultado[moeda] = spot.Value.Valor;
                continue;
            }

            CotacaoFx? ptax = await cotacaoResolver.ResolverFxAsync(
                moeda, TipoCotacao.PtaxD1, hoje, ct);

            if (ptax is not null)
            {
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
                resultado[moeda] = midRate;
            }
        }

        return resultado;
    }
}

/// <summary>
/// Carregador lazy de snapshot CDI para uso dentro do handler.
/// Evita múltiplos round-trips ao banco quando há várias simulações CdiSpread no mesmo cenário.
/// Substitui o padrão 'ref decimal?' que é incompatível com closures em C#.
/// </summary>
file sealed class CdiLoader(ICdiSnapshotRepository cdiRepo, LocalDate dataMaxima)
{
    private decimal? _valor;

    /// <summary>
    /// Retorna o CDI em % a.a. do snapshot mais recente disponível.
    /// Lança <see cref="InvalidOperationException"/> se nenhum snapshot estiver cadastrado.
    /// Resultado é memoizado — o repositório é consultado no máximo uma vez.
    /// </summary>
    public async Task<decimal?> ObterAsync(CancellationToken ct)
    {
        if (_valor.HasValue)
        {
            return _valor;
        }

        CdiSnapshot snapshot = await cdiRepo.GetMaisRecenteAsync(dataMaxima, ct)
            ?? throw new InvalidOperationException(
                "Nenhum snapshot de CDI cadastrado. " +
                "Cadastre o CDI atual via POST /api/v1/cdi-snapshots antes de usar " +
                "cenários com TipoTaxa = CdiSpread.");

        _valor = snapshot.CdiAaPercentual;
        return _valor;
    }
}
