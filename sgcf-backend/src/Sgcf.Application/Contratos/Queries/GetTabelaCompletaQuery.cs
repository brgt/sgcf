using MediatR;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Calculo;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos.Queries;

public sealed record GetTabelaCompletaQuery(Guid Id, LocalDate? DataReferencia = null, decimal? TaxaCambio = null) : IRequest<TabelaCompletaDto>;

public sealed class GetTabelaCompletaQueryHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock)
    : IRequestHandler<GetTabelaCompletaQuery, TabelaCompletaDto>
{
    public async Task<TabelaCompletaDto> Handle(GetTabelaCompletaQuery query, CancellationToken cancellationToken)
    {
        Contrato contrato = await contratoRepo.GetByIdWithDetailsAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{query.Id}' não encontrado.");

        FinimpDetail? finimpDetail = await contratoRepo.GetFinimpDetailAsync(query.Id, cancellationToken);

        IReadOnlyList<EventoCronograma> eventos = await cronogramaRepo.GetByContratoIdAsync(query.Id, cancellationToken);

        LocalDate dataRef = query.DataReferencia ?? clock.GetCurrentInstant().InUtc().Date;

        // ── Cotação aplicada para conversão BRL ───────────────────────────────
        decimal? taxaCambioEfetiva = query.TaxaCambio;
        CotacaoAplicadaDto? cotacaoAplicada = null;

        if (taxaCambioEfetiva.HasValue)
        {
            cotacaoAplicada = new CotacaoAplicadaDto("Manual", taxaCambioEfetiva.Value, null);
        }
        else if (contrato.Moeda != Moeda.Brl)
        {
            ResultadoCotacao? resultado = await cotacaoResolver.ResolveAsync(
                contrato.Moeda, contrato.BancoId, contrato.Modalidade, cancellationToken);
            if (resultado is not null)
            {
                taxaCambioEfetiva = resultado.ValorMidRate.Valor;
                cotacaoAplicada = new CotacaoAplicadaDto(
                    resultado.Tipo.ToString(),
                    resultado.ValorMidRate.Valor,
                    resultado.Momento.ToString());
            }
        }

        // ── Saldo devedor ─────────────────────────────────────────────────────
        ResultadoSaldo saldo;

        if (eventos.Count == 0)
        {
            saldo = new ResultadoSaldo(
                SaldoPrincipalAberto: contrato.ValorPrincipal,
                JurosProvisionados: Money.Zero(contrato.Moeda),
                ComissoesAPagar: Money.Zero(contrato.Moeda),
                SaldoTotal: contrato.ValorPrincipal);
        }
        else
        {
            List<EventoSaldoItem> itens = new(eventos.Count);
            foreach (EventoCronograma e in eventos)
            {
                itens.Add(new EventoSaldoItem(e.Tipo, e.Status, e.DataPrevista, e.ValorMoedaOriginal));
            }

            EntradaCalculoSaldo entrada = new(
                ValorPrincipalInicial: contrato.ValorPrincipal,
                TaxaAa: contrato.TaxaAa,
                BaseCalculo: contrato.BaseCalculo,
                DataDesembolso: contrato.DataContratacao,
                DataReferencia: dataRef,
                Eventos: itens.AsReadOnly(),
                TaxaCambio: taxaCambioEfetiva);

            saldo = CalculadorSaldo.Calcular(entrada);
        }

        // ── Indicadores operacionais ──────────────────────────────────────────
        decimal totalPrincipalPago = 0m;
        decimal totalJurosPagos = 0m;
        decimal totalComissoesPagas = 0m;

        foreach (EventoCronograma e in eventos)
        {
            if (e.Status != StatusEventoCronograma.Pago)
            {
                continue;
            }

            if (e.Tipo == TipoEventoCronograma.Principal)
            {
                totalPrincipalPago += e.ValorMoedaOriginal.Valor;
            }
            else if (e.Tipo == TipoEventoCronograma.Juros)
            {
                totalJurosPagos += e.ValorMoedaOriginal.Valor;
            }
            else
            {
                totalComissoesPagas += e.ValorMoedaOriginal.Valor;
            }
        }

        int totalEventos = eventos
            .Select(e => e.NumeroEvento)
            .Distinct()
            .Count();

        int eventosPagos = eventos
            .GroupBy(e => e.NumeroEvento)
            .Count(g => g.Any(e => e.Status == StatusEventoCronograma.Pago));

        int eventosEmAberto = eventos
            .GroupBy(e => e.NumeroEvento)
            .Count(g => g.Any(e =>
                e.Status == StatusEventoCronograma.Previsto ||
                e.Status == StatusEventoCronograma.Atrasado));

        int eventosEmAtraso = eventos
            .GroupBy(e => e.NumeroEvento)
            .Count(g => g.Any(e => e.Status == StatusEventoCronograma.Atrasado));

        decimal pctAdimplencia = totalEventos > 0
            ? Math.Round((decimal)eventosPagos / totalEventos * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        LocalDate? proximaParcelaLocal = eventos
            .Where(e =>
                (e.Status == StatusEventoCronograma.Previsto || e.Status == StatusEventoCronograma.Atrasado) &&
                e.DataPrevista > dataRef)
            .Select(e => (LocalDate?)e.DataPrevista)
            .Min();

        DateOnly? proximaParcela = proximaParcelaLocal.HasValue
            ? new DateOnly(proximaParcelaLocal.Value.Year, proximaParcelaLocal.Value.Month, proximaParcelaLocal.Value.Day)
            : (DateOnly?)null;

        decimal? valorProximaParcela = null;
        if (proximaParcelaLocal.HasValue)
        {
            decimal soma = 0m;
            foreach (EventoCronograma e in eventos)
            {
                if (e.DataPrevista == proximaParcelaLocal.Value)
                {
                    soma += e.ValorMoedaOriginal.Valor;
                }
            }

            valorProximaParcela = soma;
        }

        decimal pctPrincipalAmortizado = contrato.ValorPrincipal.Valor > 0m
            ? Math.Round(totalPrincipalPago / contrato.ValorPrincipal.Valor * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        int prazoDias = Period.Between(contrato.DataContratacao, contrato.DataVencimento, PeriodUnits.Days).Days;

        int diasDecorridosRaw = Period.Between(contrato.DataContratacao, dataRef, PeriodUnits.Days).Days;
        int diasDecorridos = Math.Clamp(diasDecorridosRaw, 0, prazoDias);

        decimal pctPrazoDecorrido = prazoDias > 0
            ? Math.Round((decimal)diasDecorridos / prazoDias * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        // ── Cronograma (Block E) ──────────────────────────────────────────────
        List<EventoCronogramaDto> cronograma = new(eventos.Count);
        foreach (EventoCronograma e in eventos)
        {
            DateOnly? dataPagEfetivo = e.DataPagamentoEfetivo.HasValue
                ? new DateOnly(e.DataPagamentoEfetivo.Value.Year, e.DataPagamentoEfetivo.Value.Month, e.DataPagamentoEfetivo.Value.Day)
                : (DateOnly?)null;

            cronograma.Add(new EventoCronogramaDto(
                NumeroEvento: e.NumeroEvento,
                Tipo: e.Tipo.ToString(),
                DataPrevista: new DateOnly(e.DataPrevista.Year, e.DataPrevista.Month, e.DataPrevista.Day),
                Valor: e.ValorMoedaOriginal.Valor,
                Moeda: e.Moeda.ToString(),
                SaldoDevedorApos: e.SaldoDevedorApos?.Valor,
                Status: e.Status.ToString(),
                DataPagamentoEfetivo: dataPagEfetivo,
                ValorPagamentoEfetivo: e.ValorPagamentoEfetivo?.Valor));
        }

        // ── Garantias (Block F) ────────────────────────────────────────────────
        List<GarantiaResumoDto> garantias = new(contrato.Garantias.Count);
        foreach (Garantia g in contrato.Garantias)
        {
            garantias.Add(new GarantiaResumoDto(
                Id: g.Id,
                Tipo: g.Tipo.ToString(),
                ValorBrl: g.ValorBrl.Valor,
                PercentualPrincipalPct: g.PercentualPrincipal?.AsHumano,
                DataConstituicao: new DateOnly(g.DataConstituicao.Year, g.DataConstituicao.Month, g.DataConstituicao.Day),
                Status: g.Status.ToString()));
        }

        // ── Histórico de pagamentos (Block H) ─────────────────────────────────
        List<PagamentoEfetivoDto> historicoPagamentos = new();
        foreach (EventoCronograma e in eventos)
        {
            if (e.Status != StatusEventoCronograma.Pago || !e.DataPagamentoEfetivo.HasValue)
            {
                continue;
            }

            historicoPagamentos.Add(new PagamentoEfetivoDto(
                NumeroEvento: e.NumeroEvento,
                Tipo: e.Tipo.ToString(),
                DataPagamentoEfetivo: new DateOnly(e.DataPagamentoEfetivo.Value.Year, e.DataPagamentoEfetivo.Value.Month, e.DataPagamentoEfetivo.Value.Day),
                ValorPagoMoedaOriginal: e.ValorPagamentoEfetivo?.Valor ?? e.ValorMoedaOriginal.Valor,
                ValorPagoBrl: e.ValorPagamentoEfetivoBrl?.Valor,
                TaxaCambioPagamento: e.TaxaCambioPagamento));
        }

        // ── Montagem do DTO principal ─────────────────────────────────────────
        IdentificacaoDto identificacao = new(
            Id: contrato.Id,
            CodigoInterno: contrato.CodigoInterno ?? contrato.NumeroExterno,
            Banco: contrato.BancoId.ToString(),
            Modalidade: contrato.Modalidade.ToString(),
            NumeroContratoExterno: contrato.NumeroExterno,
            DataContratacao: new DateOnly(contrato.DataContratacao.Year, contrato.DataContratacao.Month, contrato.DataContratacao.Day),
            DataVencimento: new DateOnly(contrato.DataVencimento.Year, contrato.DataVencimento.Month, contrato.DataVencimento.Day),
            Status: contrato.Status.ToString());

        ValoresPrincipaisDto valoresPrincipais = new(
            ValorPrincipalOriginal: contrato.ValorPrincipal.Valor,
            MoedaOriginal: contrato.Moeda.ToString());

        EncargosDto encargos = new(
            TaxaAaPct: contrato.TaxaAa.AsHumano,
            BaseCalculo: contrato.BaseCalculo.ToString(),
            AliqIrrfPct: null,
            AliqIofPct: null);

        ResumoFinanceiroDto resumoFinanceiro = new(
            TotalPrincipalPago: totalPrincipalPago,
            TotalJurosPagos: totalJurosPagos,
            TotalComissoesPagas: totalComissoesPagas,
            Moeda: contrato.Moeda.ToString(),
            SaldoPrincipalAberto: saldo.SaldoPrincipalAberto.Valor,
            JurosProvisionados: saldo.JurosProvisionados.Valor,
            ComissoesAPagar: saldo.ComissoesAPagar.Valor,
            SaldoTotalDevedor: saldo.SaldoTotal.Valor,
            SaldoPrincipalAbertoBrl: saldo.SaldoPrincipalAbertoBrl?.Valor,
            JurosProvisionadosBrl: saldo.JurosProvisionadosBrl?.Valor,
            ComissoesAPagarBrl: saldo.ComissoesAPagarBrl?.Valor,
            SaldoTotalDevedorBrl: saldo.SaldoTotalBrl?.Valor,
            TotalEventos: totalEventos,
            EventosPagos: eventosPagos,
            EventosEmAberto: eventosEmAberto,
            EventosEmAtraso: eventosEmAtraso,
            PctAdimplencia: pctAdimplencia,
            ProximaParcela: proximaParcela,
            ValorProximaParcela: valorProximaParcela,
            PctPrincipalAmortizado: pctPrincipalAmortizado,
            PctPrazoDecorrido: pctPrazoDecorrido);

        return new TabelaCompletaDto(
            Identificacao: identificacao,
            ValoresPrincipais: valoresPrincipais,
            Encargos: encargos,
            ResumoFinanceiro: resumoFinanceiro,
            Cronograma: cronograma.AsReadOnly(),
            Garantias: garantias.AsReadOnly(),
            Hedge: new HedgePlaceholderDto("Hedge não configurado nesta fase."),
            HistoricoPagamentos: historicoPagamentos.AsReadOnly(),
            CotacaoAplicada: cotacaoAplicada);
    }
}
