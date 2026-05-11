using System.Text.Json;
using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Antecipacao.Commands;

/// <summary>
/// Orquestra a simulação de antecipação: carrega o contrato e banco, monta a entrada,
/// despacha para a estratégia correta e persiste o resultado se solicitado.
/// </summary>
public sealed class SimularAntecipacaoCommandHandler(
    IContratoRepository contratoRepo,
    IBancoRepository bancoRepo,
    ISimulacaoAntecipacaoRepository simulacaoRepo,
    IClock clock)
    : IRequestHandler<SimularAntecipacaoCommand, ResultadoSimulacaoDto>
{
    public async Task<ResultadoSimulacaoDto> Handle(
        SimularAntecipacaoCommand cmd,
        CancellationToken cancellationToken)
    {
        Contrato contrato = await contratoRepo.GetByIdAsync(cmd.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{cmd.ContratoId}' não encontrado.");

        Banco banco = await bancoRepo.GetByIdAsync(contrato.BancoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco com Id '{contrato.BancoId}' não encontrado.");

        EntradaSimulacaoAntecipacao entrada = MontarEntrada(cmd, contrato);

        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;

        (bool restricoesPermitem, IReadOnlyList<string> alertasRestricao) =
            AntecipacaoValidador.Validar(banco, entrada, cmd.DataEfetiva, hoje);

        ResultadoSimulacaoAntecipacao resultado = AntecipacaoStrategyDispatcher.Calcular(
            banco.PadraoAntecipacao,
            entrada,
            banco);

        // Merge restriction alerts (prepended) with strategy alerts
        IReadOnlyList<string> alertasMerged = [..alertasRestricao, ..resultado.Alertas];
        bool permitidoFinal = restricoesPermitem && resultado.Permitido;

        ResultadoSimulacaoAntecipacao resultadoFinal = resultado with
        {
            Permitido = permitidoFinal,
            Alertas = alertasMerged,
        };

        Guid? simulacaoId = null;

        if (cmd.SalvarSimulacao)
        {
            simulacaoId = await PersistirSimulacaoAsync(cmd, contrato, resultadoFinal, cancellationToken);
        }

        return MapearDto(simulacaoId, resultadoFinal, entrada, cmd);
    }

    private static EntradaSimulacaoAntecipacao MontarEntrada(SimularAntecipacaoCommand cmd, Contrato contrato)
    {
        Moeda moeda = contrato.Moeda;

        // Para liquidação total, usa o principal completo do contrato.
        // Para liquidação parcial / amortização extraordinária, usa o valor informado no comando.
        bool ehParcial = cmd.TipoAntecipacao is TipoAntecipacao.LiquidacaoParcialReducaoPrazo
                      or TipoAntecipacao.LiquidacaoParcialReducaoParcela
                      or TipoAntecipacao.AmortizacaoExtraordinariaAvulsa;

        decimal principalDecimal = ehParcial && cmd.ValorPrincipalAQuitarMoedaOriginal.HasValue
            ? cmd.ValorPrincipalAQuitarMoedaOriginal.Value
            : contrato.ValorPrincipal.Valor;

        Money principalAQuitar = new(principalDecimal, moeda);

        // Juros pro rata: principal × taxa_aa × dias_desde_ultima_parcela / base
        // Na ausência de cronograma pago, assume que os juros correm desde a data de contratação.
        int diasAcumulados = Period.Between(contrato.DataContratacao, cmd.DataEfetiva, PeriodUnits.Days).Days;
        diasAcumulados = Math.Max(0, diasAcumulados);

        decimal jurosProRataDecimal = Math.Round(
            principalDecimal * contrato.TaxaAa.AsDecimal * diasAcumulados / (decimal)contrato.BaseCalculo,
            6,
            MidpointRounding.AwayFromZero);

        Money jurosProRata = new(jurosProRataDecimal, moeda);

        int prazoTotalOriginalDias = Period.Between(
            contrato.DataContratacao,
            contrato.DataVencimento,
            PeriodUnits.Days).Days;

        int prazoRemanescenteDias = Period.Between(
            cmd.DataEfetiva,
            contrato.DataVencimento,
            PeriodUnits.Days).Days;

        prazoRemanescenteDias = Math.Max(0, prazoRemanescenteDias);

        // Arredondamento para cima — cada mês parcial conta como mês inteiro para cálculo de TLA
        int prazoRemanescenteMeses = (int)Math.Ceiling(prazoRemanescenteDias / 30.0);

        Percentual? taxaMercadoAtualAa = cmd.TaxaMercadoAtualAa.HasValue
            ? Percentual.De(cmd.TaxaMercadoAtualAa.Value)
            : (Percentual?)null;

        Money? indenizacaoBanco = cmd.IndenizacaoBancoMoedaOriginal.HasValue
            ? new Money(cmd.IndenizacaoBancoMoedaOriginal.Value, moeda)
            : (Money?)null;

        return new EntradaSimulacaoAntecipacao(
            Tipo: cmd.TipoAntecipacao,
            PrincipalAQuitar: principalAQuitar,
            JurosProRata: jurosProRataDecimal > 0m ? jurosProRata : (Money?)null,
            PrazoTotalOriginalDias: prazoTotalOriginalDias,
            PrazoRemanescenteDias: prazoRemanescenteDias,
            PrazoRemanescenteMeses: prazoRemanescenteMeses,
            TaxaAa: contrato.TaxaAa,
            BaseCalculo: (int)contrato.BaseCalculo,
            TaxaMercadoAtualAa: taxaMercadoAtualAa,
            IndenizacaoBanco: indenizacaoBanco,
            OrigemRefinanciamentoInterno: cmd.TipoAntecipacao == TipoAntecipacao.RefinanciamentoInterno);
    }

    private async Task<Guid> PersistirSimulacaoAsync(
        SimularAntecipacaoCommand cmd,
        Contrato contrato,
        ResultadoSimulacaoAntecipacao resultado,
        CancellationToken cancellationToken)
    {
        // Para moeda estrangeira, o total em BRL requer cotação.
        // Na ausência de cotação informada, armazena o mesmo valor (BRL ou sem conversão).
        Money totalBrl = contrato.Moeda == Moeda.Brl
            ? resultado.TotalAQuitar
            : new Money(resultado.TotalAQuitar.Valor, Moeda.Brl);

        string componentesJson = JsonSerializer.Serialize(
            resultado.Componentes.Select(c => new
            {
                codigo = c.Codigo,
                descricao = c.Descricao,
                valor = c.Valor.Valor,
                moeda = c.Valor.Moeda.ToString(),
                sinal = c.Sinal,
            }));

        SimulacaoAntecipacao simulacao = SimulacaoAntecipacao.Criar(
            contratoId: cmd.ContratoId,
            tipo: cmd.TipoAntecipacao,
            dataEfetivaProposta: cmd.DataEfetiva,
            principalAQuitar: resultado.Componentes
                .Where(c => c.Codigo == "C1")
                .Select(c => c.Valor)
                .FirstOrDefault(Money.Zero(contrato.Moeda)),
            totalSimuladoBrl: totalBrl,
            cotacaoAplicada: null,
            taxaMercadoAtualAa: cmd.TaxaMercadoAtualAa,
            padrao: resultado.Padrao,
            componentesCustoJson: componentesJson,
            economiaEstimadaBrl: null,
            observacoesBanco: contrato.Observacoes,
            createdBy: cmd.CreatedBy,
            source: cmd.Source,
            clock: clock);

        await simulacaoRepo.AddAsync(simulacao, cancellationToken);
        return simulacao.Id;
    }

    private static ResultadoSimulacaoDto MapearDto(
        Guid? simulacaoId,
        ResultadoSimulacaoAntecipacao resultado,
        EntradaSimulacaoAntecipacao entrada,
        SimularAntecipacaoCommand cmd)
    {
        List<ComponenteCustoDto> componentesDto = new(resultado.Componentes.Count);
        foreach (ComponenteCusto comp in resultado.Componentes)
        {
            componentesDto.Add(new ComponenteCustoDto(
                Codigo: comp.Codigo,
                Descricao: comp.Descricao,
                ValorMoedaOriginal: comp.Valor.Valor,
                Sinal: comp.Sinal));
        }

        ComparativoSimulacaoDto comparativo = ComputarComparativo(entrada, resultado);

        return new ResultadoSimulacaoDto(
            SimulacaoId: simulacaoId,
            PadraoAplicado: resultado.Padrao.ToString(),
            Permitido: resultado.Permitido,
            Alertas: resultado.Alertas,
            ComponentesCusto: componentesDto.AsReadOnly(),
            TotalAPagarMoedaOriginal: resultado.TotalAQuitar.Valor,
            TotalAPagarBrl: resultado.TotalAQuitar.Valor,
            CotacaoAplicada: null,
            Comparativo: comparativo);
    }

    private static ComparativoSimulacaoDto ComputarComparativo(
        EntradaSimulacaoAntecipacao entrada,
        ResultadoSimulacaoAntecipacao resultado)
    {
        // Estimativa do custo se NÃO antecipar: principal + juros restantes até o vencimento (juros simples)
        decimal jurosRestantes = Math.Round(
            entrada.PrincipalAQuitar.Valor * entrada.TaxaAa.AsDecimal
                * entrada.PrazoRemanescenteDias / (decimal)entrada.BaseCalculo,
            6,
            MidpointRounding.AwayFromZero);

        decimal custoSeNaoAntecipar = Math.Round(
            entrada.PrincipalAQuitar.Valor + jurosRestantes,
            6,
            MidpointRounding.AwayFromZero);

        decimal diferenca = Math.Round(
            resultado.TotalAQuitar.Valor - custoSeNaoAntecipar,
            6,
            MidpointRounding.AwayFromZero);

        bool melhorAntecipar = diferenca < 0m;
        string decisao = melhorAntecipar ? "ANTECIPAR" : "NAO_ANTECIPAR";

        string justificativa = melhorAntecipar
            ? $"Antecipar custa {resultado.TotalAQuitar.Valor:N2} vs manter até o vencimento ({custoSeNaoAntecipar:N2}). Economia estimada: {Math.Abs(diferenca):N2} {entrada.PrincipalAQuitar.Moeda}."
            : $"Manter até o vencimento custa {custoSeNaoAntecipar:N2} vs antecipar ({resultado.TotalAQuitar.Valor:N2}). Antecipar é {diferenca:N2} {entrada.PrincipalAQuitar.Moeda} mais caro.";

        return new ComparativoSimulacaoDto(
            CustoSeNaoAnteciparMoedaOriginal: custoSeNaoAntecipar,
            DiferencaMoedaOriginal: diferenca,
            DecisaoOtima: decisao,
            Justificativa: justificativa);
    }
}
