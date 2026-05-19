using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Painel;

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
public sealed record GetQuadroDividaQuery(int Ano) : IRequest<QuadroDividaDto>;

/// <summary>
/// Handler do <see cref="GetQuadroDividaQuery"/>.
///
/// Orquestra:
///   1. Saldo inicial via <see cref="GetSaldoPorBancoAtualQuery"/> (reutiliza Task 1.2).
///   2. Contratos ativos para montar o mapa <c>ContratoId → BancoId</c>.
///   3. Eventos de amortização de principal do cronograma para o ano.
///   4. Conversão para <see cref="EventoProjecao"/> com taxa BRL corrente.
///   5. Invocação do <see cref="ProjetorSaldoMensal"/> (função pura AD-5).
///   6. Mapeamento para DTOs com <c>bancoApelido</c> (AD-10).
///   7. Cálculo do sumário anual.
///
/// Nota sobre <c>IContratoRepository</c>: o campo <c>EventoCronograma</c> não guarda
/// <c>BancoId</c> diretamente — o vínculo é via <c>ContratoId</c>. O repositório de contratos
/// é necessário para construir o mapa <c>ContratoId → BancoId</c>.
/// </summary>
public sealed class GetQuadroDividaQueryHandler(
    IMediator mediator,
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    IBancoRepository bancoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock) : IRequestHandler<GetQuadroDividaQuery, QuadroDividaDto>
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

        // 6. Projetor puro (AD-5) — retorna exatamente 12 MesProjecao
        QuadroDividaProjecao projecao = ProjetorSaldoMensal.Projetar(
            saldoInicialPorBanco,
            eventosProjecao,
            request.Ano);

        // 7. Apelidos dos bancos para enriquecer DTOs (AD-10)
        IReadOnlyList<Banco> bancos = await bancoRepo.ListAllAsync(cancellationToken);
        Dictionary<Guid, string> apelidos = bancos.ToDictionary(b => b.Id, b => b.Apelido);

        // 8. Mapeia domain → DTOs
        QuadroDividaProjecaoDto projecaoDto = MapearProjecaoDto(projecao, apelidos);
        QuadroDividaSumarioDto sumario = CalcularSumario(projecao);

        LocalDate dataRef = snapshot.DataReferencia;
        DateOnly dataReferencia = new(dataRef.Year, dataRef.Month, dataRef.Day);

        return new QuadroDividaDto(
            Ano: request.Ano,
            DataReferencia: dataReferencia,
            SnapshotInicial: snapshot,
            Projecao: projecaoDto,
            Sumario: sumario,
            Alertas: []);
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
