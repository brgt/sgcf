using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
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
/// Nota sobre cache (<see cref="ICronogramaSimulacaoCache"/>): opcional — null é aceito quando
/// Redis não está configurado no ambiente. Sem cache, o cronograma é calculado on-the-fly.
/// </summary>
public sealed class GetQuadroDividaQueryHandler(
    IMediator mediator,
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    IBancoRepository bancoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock,
    ICenarioSimulacaoRepository cenarioRepo,
    IServiceProvider serviceProvider,
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

        int anoCorrente = clock.GetCurrentInstant().InZone(FusoBrasilia).Year;
        ValidarAnoCorrenteMvp(request.Ano, anoCorrente);

        // 1. Snapshot atual da carteira por banco (via GetSaldoPorBancoAtualQuery — Task 1.2)
        SaldoPorBancoAtualDto snapshot = await mediator.Send(
            new GetSaldoPorBancoAtualQuery(), cancellationToken);

        // 2. Contratos ativos para construir o mapa ContratoId → BancoId
        //    (EventoCronograma vincula-se por ContratoId, não por BancoId)
        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        List<Contrato> ativos = contratos.Where(c => c.Status == StatusContrato.Ativo).ToList();

        Dictionary<Guid, Guid> bancoIdPorContrato = ativos.ToDictionary(c => c.Id, c => c.BancoId);
        Dictionary<Guid, Moeda> moedaPorContrato = ativos.ToDictionary(c => c.Id, c => c.Moeda);

        // 3. Saldo inicial por banco como dicionário BancoId → Money(BRL)
        //    Vem do snapshot — garante consistência com PainelDivida.DividaBrutaBrl (AD-8)
        Dictionary<Guid, Money> saldoInicialPorBanco = snapshot.Bancos
            .ToDictionary(
                b => b.BancoId,
                b => new Money(b.SaldoBrl, Moeda.Brl));

        // 4. Eventos de amortização de principal do cronograma para o ano
        IReadOnlyList<Guid> contratoIds = ativos.Select(c => c.Id).ToList().AsReadOnly();

        IReadOnlyList<EventoCronograma> eventosDb =
            await cronogramaRepo.ListAbertosParaAnoAsync(request.Ano, contratoIds, cancellationToken);

        // 5. Converte para EventoProjecao usando taxa BRL corrente (spot → PTAX D-1 mid-rate)
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        IReadOnlySet<Moeda> moedasEstrangeiras = ativos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxas =
            await ResolverCotacoesAsync(moedasEstrangeiras, hoje, cancellationToken);

        List<EventoProjecao> eventosProjecao = ConverterParaEventosProjecao(
            eventosDb,
            bancoIdPorContrato,
            moedaPorContrato,
            taxas);

        // 6. (Fase 3) Incorporar eventos do cenário de simulação — AD-9
        CenarioAplicadoDto? cenarioAplicadoDto = null;

        if (request.CenarioId.HasValue)
        {
            CenarioSimulacao cenario = await cenarioRepo.GetByIdAsync(request.CenarioId.Value, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Cenário de simulação '{request.CenarioId.Value}' não encontrado.");

            ValidarAnoBaseCenario(cenario, request.Ano);

            // CDI lazy: carregado no máximo uma vez por chamada, somente se alguma simulação usar CdiSpread.
            // Usamos um CdiLoader com estado interno para evitar 'ref' em closure.
            var cdiLoader = new CdiLoader(cdiRepo, hoje);

            List<EventoProjecao> eventosCenario = await GerarEventosDoCenarioAsync(
                cenario,
                request.Ano,
                () => cdiLoader.ObterAsync(cancellationToken),
                cancellationToken);

            eventosProjecao.AddRange(eventosCenario);

            cenarioAplicadoDto = new CenarioAplicadoDto(
                Id: cenario.Id,
                Nome: cenario.Nome,
                Status: cenario.Status.ToString(),
                AnoBase: cenario.AnoBase,
                QuantidadeSimulacoes: cenario.Simulacoes.Count);
        }

        // 7. Projetor puro (AD-5) — retorna exatamente 12 MesProjecao
        QuadroDividaProjecao projecao = ProjetorSaldoMensal.Projetar(
            saldoInicialPorBanco,
            eventosProjecao,
            request.Ano);

        // 8. Apelidos dos bancos para enriquecer DTOs (AD-10)
        IReadOnlyList<Banco> bancos = await bancoRepo.ListAllAsync(cancellationToken);
        Dictionary<Guid, string> apelidos = bancos.ToDictionary(b => b.Id, b => b.Apelido);

        // 9. Mapeia domain → DTOs
        QuadroDividaProjecaoDto projecaoDto = MapearProjecaoDto(projecao, apelidos);
        QuadroDividaSumarioDto sumario = CalcularSumario(projecao);

        // 10. (Task 3.4 — D-11) Alertas de tetão mensal: pure function, nenhuma I/O adicional
        Domain.Sistema.ParametroSistema parametros =
            await parametroSistemaRepo.GetOrCreateGlobalAsync(clock, cancellationToken);

        IReadOnlyList<string> alertasTetao = ValidadorTetaoMensal.Validar(
            projecaoDto,
            parametros.TetaoMensalCapacidadeBrl?.Valor);

        LocalDate dataRef = snapshot.DataReferencia;
        DateOnly dataReferencia = new(dataRef.Year, dataRef.Month, dataRef.Day);

        return new QuadroDividaDto(
            Ano: request.Ano,
            DataReferencia: dataReferencia,
            SnapshotInicial: snapshot,
            Projecao: projecaoDto,
            Sumario: sumario,
            Alertas: alertasTetao,
            CenarioAplicado: cenarioAplicadoDto);
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
    /// O cache Redis (<see cref="ICronogramaSimulacaoCache"/>) é usado quando disponível.
    /// Quando null (Redis ausente no ambiente), o cronograma é calculado on-the-fly.
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

            // Cronograma hipotético — via cache Redis se disponível, on-the-fly caso contrário (AD-3).
            // GetService retorna null quando ICronogramaSimulacaoCache não está registrado (Redis ausente).
            ICronogramaSimulacaoCache? cache = serviceProvider.GetService<ICronogramaSimulacaoCache>();
            IReadOnlyList<EventoCronogramaGerado> cronograma = cache is not null
                ? await cache.GetOrCreateAsync(
                    cenario.Id,
                    simulacao.Id,
                    simulacao.Version,
                    () => Task.FromResult(SimulacaoCronogramaCalculator.Calcular(simulacao, cdiAa)),
                    ct)
                : SimulacaoCronogramaCalculator.Calcular(simulacao, cdiAa);

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
                    SaldoInicio: b.SaldoInicio.Valor,
                    SaldoFim: b.SaldoFim.Valor,
                    TotalAmortizacaoNoMes: b.TotalAmortizacaoNoMes.Valor,
                    TotalCaptacaoNoMes: b.TotalCaptacaoNoMes.Valor,
                    SharePercentual: b.SharePercentual))
                .ToList();

            decimal totalAmortizacaoMes = bancosDto.Sum(b => b.TotalAmortizacaoNoMes);
            decimal totalCaptacaoMes = bancosDto.Sum(b => b.TotalCaptacaoNoMes);

            meses.Add(new MesProjecaoDto(
                Ano: mes.AnoCalendar,
                Mes: mes.Mes,
                Bancos: bancosDto.AsReadOnly(),
                SaldoTotalInicio: mes.SaldoTotalInicio.Valor,
                SaldoTotalFim: mes.SaldoTotalFim.Valor,
                TotalAmortizacaoMes: totalAmortizacaoMes,
                TotalCaptacaoMes: totalCaptacaoMes));
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
            SaldoTotalInicioAno: saldoInicioAno,
            SaldoTotalFimAno: saldoFimAno,
            TotalAmortizacaoNoAno: totalAmortizacao,
            TotalCaptacaoNoAno: totalCaptacao,
            VariacaoAnualPercentual: variacao);
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

            CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
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
