using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Tenancy;


namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Limite operacional de um banco para uma modalidade.
/// Aggregate independente de Cotacao. Controla o teto de exposição permitido.
/// SPEC §3.1.
/// </summary>
public sealed class LimiteBanco : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid BancoId { get; private set; }
    public ModalidadeContrato Modalidade { get; private set; }

    internal decimal ValorLimiteBrlDecimal { get; private set; }
    public Money ValorLimiteBrl => new(ValorLimiteBrlDecimal, Moeda.Brl);

    internal decimal ValorUtilizadoBrlDecimal { get; private set; }
    public Money ValorUtilizadoBrl => new(ValorUtilizadoBrlDecimal, Moeda.Brl);

    /// <summary>Disponível = Limite − Utilizado. Propriedade computada, nunca persista diretamente.</summary>
    public Money ValorDisponivelBrl =>
        new(Math.Max(0m, ValorLimiteBrlDecimal - ValorUtilizadoBrlDecimal), Moeda.Brl);

    public LocalDate DataVigenciaInicio { get; private set; }
    public LocalDate? DataVigenciaFim { get; private set; }
    public string? Observacoes { get; private set; }

    // ── Configuração de antecipação por modalidade ───────────────────────────
    // Movido de Banco para cá (S32): o padrão e os parâmetros de cálculo variam
    // por banco E modalidade — o mesmo banco pode ter fórmulas distintas (ex: D vs E).

    /// <summary>Padrão de antecipação configurado para este limite (banco+modalidade).</summary>
    public PadraoAntecipacao? PadraoAntecipacao { get; private set; }

    internal decimal? BreakFundingFeePctDecimal { get; private set; }
    public Percentual? BreakFundingFeePct =>
        BreakFundingFeePctDecimal.HasValue ? Percentual.DeFracao(BreakFundingFeePctDecimal.Value) : null;

    internal decimal? TlaPctSobreSaldoDecimal { get; private set; }
    public Percentual? TlaPctSobreSaldo =>
        TlaPctSobreSaldoDecimal.HasValue ? Percentual.DeFracao(TlaPctSobreSaldoDecimal.Value) : null;

    internal decimal? TlaPctPorMesRemanescenteDecimal { get; private set; }
    public Percentual? TlaPctPorMesRemanescente =>
        TlaPctPorMesRemanescenteDecimal.HasValue ? Percentual.DeFracao(TlaPctPorMesRemanescenteDecimal.Value) : null;

    internal decimal? ValorMinimoParcialPctDecimal { get; private set; }
    public Percentual? ValorMinimoParcialPct =>
        ValorMinimoParcialPctDecimal.HasValue ? Percentual.DeFracao(ValorMinimoParcialPctDecimal.Value) : null;

    public string? ObservacoesAntecipacao { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private readonly List<GarantiaExigidaRevisao> _revisoesGarantias = new();
    private readonly List<LimiteBancoHistorico> _historico = new();

    /// <summary>
    /// Histórico de todas as revisões de garantias exigidas (vigentes e encerradas).
    /// Append-only — nenhuma revisão é removida. SPEC §3.2.
    /// </summary>
    public IReadOnlyCollection<GarantiaExigidaRevisao> RevisoesGarantiasExigidas
        => _revisoesGarantias.AsReadOnly();

    /// <summary>
    /// Revisão de garantias vigente (VigenciaFim IS NULL).
    /// Null se nunca houve revisão cadastrada (banco sem política formal de garantia).
    /// SLB-01: no máximo uma vigente por LimiteBanco.
    /// </summary>
    public GarantiaExigidaRevisao? RevisaoGarantiasVigente
        // SingleOrDefault é intencional: lança InvalidOperationException se houver 2+ vigentes,
        // expondo imediatamente uma violação SLB-01 em vez de mascarar a corrupção.
        => _revisoesGarantias.SingleOrDefault(r => r.VigenciaFim is null);

    /// <summary>
    /// Revisão vigente em relação a um instante exclusivo de fim de período.
    /// <para>
    /// <paramref name="fimExclusivo"/> deve ser o início do dia seguinte ao dia de referência
    /// (ex.: <c>dataContratacao.PlusDays(1).AtStartOfDayInZone(TZ).ToInstant()</c>).
    /// A revisão é considerada vigente se <c>VigenciaInicio &lt; fimExclusivo</c>
    /// e <c>VigenciaFim == null || VigenciaFim &gt;= fimExclusivo</c>.
    /// </para>
    /// </summary>
    public GarantiaExigidaRevisao? RevisaoVigenteEm(Instant fimExclusivo) =>
        _revisoesGarantias.FirstOrDefault(r =>
            r.VigenciaInicio < fimExclusivo &&
            (r.VigenciaFim is null || r.VigenciaFim.Value >= fimExclusivo));

    /// <summary>
    /// Itens da revisão vigente. Coleção vazia se não houver revisão.
    /// Mantém o nome <c>GarantiasExigidas</c> por compatibilidade da API existente.
    /// SPEC §3.2.
    /// </summary>
    public IReadOnlyCollection<GarantiaExigidaItem> GarantiasExigidas
        => RevisaoGarantiasVigente?.Itens ?? Array.Empty<GarantiaExigidaItem>();

    /// <summary>
    /// Histórico de alterações do valor do limite concedido pelo banco.
    /// Cada mudança de <see cref="ValorLimiteBrl"/> registra uma entrada para análise de tendência.
    /// </summary>
    public IReadOnlyCollection<LimiteBancoHistorico> Historico => _historico.AsReadOnly();

    /// <summary>Construtor privado para EF Core.</summary>
    private LimiteBanco() { }

    /// <summary>
    /// Cria novo limite operacional para banco/modalidade.
    /// Invariante: ValorUtilizado inicial é zero; ValorLimite deve ser positivo.
    /// </summary>
    public static LimiteBanco Criar(
        Guid bancoId,
        ModalidadeContrato modalidade,
        Money valorLimiteBrl,
        LocalDate dataVigenciaInicio,
        IClock clock,
        LocalDate? dataVigenciaFim = null,
        string? observacoes = null,
        PadraoAntecipacao? padraoAntecipacao = null,
        IEnumerable<GarantiaExigidaItemSpec>? garantiasExigidas = null)
    {
        if (valorLimiteBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorLimiteBrl deve ser em BRL.", nameof(valorLimiteBrl));
        }

        if (valorLimiteBrl.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valorLimiteBrl), "ValorLimiteBrl deve ser positivo.");
        }

        if (dataVigenciaFim.HasValue && dataVigenciaFim.Value <= dataVigenciaInicio)
        {
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(dataVigenciaFim));
        }

        var now = clock.GetCurrentInstant();
        var limite = new LimiteBanco
        {
            BancoId = bancoId,
            Modalidade = modalidade,
            ValorLimiteBrlDecimal = valorLimiteBrl.Valor,
            ValorUtilizadoBrlDecimal = 0m,
            DataVigenciaInicio = dataVigenciaInicio,
            DataVigenciaFim = dataVigenciaFim,
            Observacoes = observacoes,
            PadraoAntecipacao = padraoAntecipacao,
            CreatedAt = now,
            UpdatedAt = now,
        };

        limite._historico.Add(LimiteBancoHistorico.Criar(
            limiteBancoId: limite.Id,
            valorAnteriorBrl: null,
            valorNovoBrl: valorLimiteBrl,
            registradoEm: now,
            observacoes: "Criação do limite"));

        var specs = garantiasExigidas?.ToList();
        if (specs is { Count: > 0 })
        {
            ValidarSemDuplicadosPorTipo(specs);
            var revisaoInicial = GarantiaExigidaRevisao.CriarComInstant(
                limiteBancoId: limite.Id,
                itens: specs,
                momento: now,
                motivo: "Criação do limite");
            limite._revisoesGarantias.Add(revisaoInicial);
        }

        return limite;
    }

    /// <summary>
    /// Configura os parâmetros de antecipação específicos desta modalidade.
    /// Substitui os valores anteriores integralmente (semântica replace).
    /// Passe <c>null</c> em qualquer parâmetro para deixá-lo sem valor.
    /// </summary>
    public void ConfigurarAntecipacao(
        PadraoAntecipacao? padraoAntecipacao,
        decimal? breakFundingFeePct,
        decimal? tlaPctSobreSaldo,
        decimal? tlaPctPorMesRemanescente,
        decimal? valorMinimoParcialPct,
        string? observacoesAntecipacao,
        IClock clock)
    {
        PadraoAntecipacao = padraoAntecipacao;
        BreakFundingFeePctDecimal = breakFundingFeePct;
        TlaPctSobreSaldoDecimal = tlaPctSobreSaldo;
        TlaPctPorMesRemanescenteDecimal = tlaPctPorMesRemanescente;
        ValorMinimoParcialPctDecimal = valorMinimoParcialPct;
        ObservacoesAntecipacao = observacoesAntecipacao;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Substitui as garantias exigidas: fecha a revisão vigente (se houver)
    /// e abre uma nova com os itens fornecidos. Append-only.
    /// SLB-04: se a lista nova for equivalente à vigente, nenhuma revisão é criada.
    /// SLB-02+SLB-03: fecha e abre com o mesmo Instant para garantir continuidade temporal.
    /// </summary>
    public void SubstituirGarantiasExigidas(
        IEnumerable<GarantiaExigidaItemSpec> novas,
        IClock clock,
        string? motivo = null,
        string? observacoes = null)
    {
        ArgumentNullException.ThrowIfNull(novas);

        var listaNova = novas.ToList();
        ValidarSemDuplicadosPorTipo(listaNova);

        // SLB-04: idempotência por valor — se a lista nova é equivalente à vigente, não cria revisão.
        var vigente = RevisaoGarantiasVigente;
        if (vigente is not null && PoliticasEquivalentes(vigente.Itens, listaNova))
        {
            return;
        }

        // SLB-02+SLB-03: captura o Instant uma única vez para garantir continuidade sem gap.
        var now = clock.GetCurrentInstant();

        vigente?.EncerrarVigencia(now);

        var novaRevisao = GarantiaExigidaRevisao.CriarComInstant(
            limiteBancoId: Id,
            itens: listaNova,
            momento: now,
            motivo: motivo,
            observacoes: observacoes);

        _revisoesGarantias.Add(novaRevisao);
        UpdatedAt = now;
    }

    /// <summary>
    /// Adiciona uma garantia exigida. Se há revisão vigente, fecha e abre nova com
    /// (itens da anterior + novo item). Se não há revisão, abre a primeira. SR-06.
    /// </summary>
    public void AdicionarGarantiaExigida(
        GarantiaExigidaItemSpec spec,
        IClock clock,
        string? motivo = null)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var vigente = RevisaoGarantiasVigente;

        // SR-06: valida duplicidade antes de criar a nova revisão.
        if (vigente is not null && vigente.Itens.Any(i => i.Tipo == spec.Tipo))
        {
            throw new InvalidOperationException(
                $"Garantia exigida do tipo {spec.Tipo} já está cadastrada (duplicada) no limite {Id}.");
        }

        var now = clock.GetCurrentInstant();

        var itensNovos = (vigente?.Itens ?? Array.Empty<GarantiaExigidaItem>())
            .Select(i => new GarantiaExigidaItemSpec(
                i.Tipo, i.PercentualSobreLimite, i.ValorFixoBrl, i.Obrigatoria, i.Observacoes))
            .Append(spec)
            .ToList();

        vigente?.EncerrarVigencia(now);

        var novaRevisao = GarantiaExigidaRevisao.CriarComInstant(
            limiteBancoId: Id,
            itens: itensNovos,
            momento: now,
            motivo: motivo);

        _revisoesGarantias.Add(novaRevisao);
        UpdatedAt = now;
    }

    /// <summary>
    /// Remove uma garantia exigida pelo Tipo. Fecha a revisão vigente e abre nova
    /// com (itens da anterior − tipo informado).
    /// Lança se não houver revisão vigente ou se o tipo não estiver presente.
    /// </summary>
    public void RemoverGarantiaExigidaPorTipo(
        TipoGarantia tipo,
        IClock clock,
        string? motivo = null)
    {
        var vigente = RevisaoGarantiasVigente
            ?? throw new InvalidOperationException(
                $"Nenhuma revisão vigente no limite {Id}. Não é possível remover garantia.");

        var itemARemover = vigente.Itens.FirstOrDefault(i => i.Tipo == tipo)
            ?? throw new InvalidOperationException(
                $"Garantia exigida do tipo {tipo} não encontrada na revisão vigente do limite {Id}.");

        var now = clock.GetCurrentInstant();

        var itensSemRemovido = vigente.Itens
            .Where(i => i.Tipo != tipo)
            .Select(i => new GarantiaExigidaItemSpec(
                i.Tipo, i.PercentualSobreLimite, i.ValorFixoBrl, i.Obrigatoria, i.Observacoes))
            .ToList();

        vigente.EncerrarVigencia(now);

        var novaRevisao = GarantiaExigidaRevisao.CriarComInstant(
            limiteBancoId: Id,
            itens: itensSemRemovido,
            momento: now,
            motivo: motivo);

        _revisoesGarantias.Add(novaRevisao);
        UpdatedAt = now;
    }

    private static bool PoliticasEquivalentes(
        IReadOnlyCollection<GarantiaExigidaItem> atuais,
        List<GarantiaExigidaItemSpec> novas)
    {
        if (atuais.Count != novas.Count)
        {
            return false;
        }

        var porTipoAtuais = atuais.ToDictionary(i => i.Tipo);
        foreach (var nova in novas)
        {
            if (!porTipoAtuais.TryGetValue(nova.Tipo, out var atual))
            {
                return false;
            }

            if (atual.PercentualSobreLimite != nova.PercentualSobreLimite)
            {
                return false;
            }

            if (atual.ValorFixoBrl?.Valor != nova.ValorFixoBrl?.Valor)
            {
                return false;
            }

            if (atual.Obrigatoria != nova.Obrigatoria)
            {
                return false;
            }

            if (atual.Observacoes != nova.Observacoes)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidarSemDuplicadosPorTipo(IEnumerable<GarantiaExigidaItemSpec> specs)
    {
        var tiposDuplicados = specs
            .GroupBy(s => s.Tipo)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (tiposDuplicados.Count > 0)
        {
            throw new InvalidOperationException(
                $"Garantia exigida duplicada para o(s) tipo(s): {string.Join(", ", tiposDuplicados)}.");
        }
    }

    /// <summary>
    /// Atualiza campos do limite (valor e datas de vigência).
    /// Invariante: novo limite não pode ser menor que valor já utilizado.
    /// </summary>
    public void Atualizar(
        IClock clock,
        Money? novoLimiteBrl = null,
        LocalDate? novaDataVigenciaInicio = null,
        LocalDate? novaDataVigenciaFim = null,
        string? observacoes = null)
    {
        if (novoLimiteBrl.HasValue)
        {
            if (novoLimiteBrl.Value.Moeda != Moeda.Brl)
            {
                throw new ArgumentException("NovoLimiteBrl deve ser em BRL.", nameof(novoLimiteBrl));
            }

            if (novoLimiteBrl.Value.Valor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(novoLimiteBrl), "NovoLimiteBrl deve ser positivo.");
            }

            if (novoLimiteBrl.Value.Valor < ValorUtilizadoBrlDecimal)
            {
                throw new InvalidOperationException(
                    $"Novo limite (BRL {novoLimiteBrl.Value.Valor:F2}) é menor que o valor já utilizado (BRL {ValorUtilizadoBrlDecimal:F2}).");
            }

            if (novoLimiteBrl.Value.Valor != ValorLimiteBrlDecimal)
            {
                var valorAnterior = new Money(ValorLimiteBrlDecimal, Moeda.Brl);
                ValorLimiteBrlDecimal = novoLimiteBrl.Value.Valor;
                _historico.Add(LimiteBancoHistorico.Criar(
                    limiteBancoId: Id,
                    valorAnteriorBrl: valorAnterior,
                    valorNovoBrl: novoLimiteBrl.Value,
                    registradoEm: clock.GetCurrentInstant(),
                    observacoes: observacoes));
            }
        }

        LocalDate vigenciaInicio = novaDataVigenciaInicio ?? DataVigenciaInicio;
        LocalDate? vigenciaFim = novaDataVigenciaFim ?? DataVigenciaFim;

        if (vigenciaFim.HasValue && vigenciaFim.Value <= vigenciaInicio)
        {
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(novaDataVigenciaFim));
        }

        if (novaDataVigenciaInicio.HasValue)
        {
            DataVigenciaInicio = novaDataVigenciaInicio.Value;
        }

        if (novaDataVigenciaFim.HasValue)
        {
            DataVigenciaFim = novaDataVigenciaFim;
        }

        if (observacoes is not null)
        {
            Observacoes = observacoes;
        }

        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Incrementa o valor utilizado ao confirmar uso do limite (ex: conversão em contrato).
    /// Invariante: ValorUtilizado ≤ ValorLimite.
    /// </summary>
    public void RegistrarUso(Money valor, IClock clock)
    {
        if (valor.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("Valor deve ser em BRL.", nameof(valor));
        }

        if (valor.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor de uso deve ser positivo.");
        }

        decimal novoUtilizado = ValorUtilizadoBrlDecimal + valor.Valor;
        if (novoUtilizado > ValorLimiteBrlDecimal)
        {
            throw new InvalidOperationException(
                $"Uso de BRL {valor.Valor:F2} excederia o limite de BRL {ValorLimiteBrlDecimal:F2} (utilizado atual: BRL {ValorUtilizadoBrlDecimal:F2}).");
        }

        ValorUtilizadoBrlDecimal = novoUtilizado;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Decrementa o valor utilizado ao liberar uso (ex: liquidação ou cancelamento de contrato).
    /// Não permite valor utilizado negativo.
    /// </summary>
    public void LiberarUso(Money valor, IClock clock)
    {
        if (valor.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("Valor deve ser em BRL.", nameof(valor));
        }

        if (valor.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor de liberação deve ser positivo.");
        }

        decimal novoUtilizado = ValorUtilizadoBrlDecimal - valor.Valor;
        if (novoUtilizado < 0)
        {
            throw new InvalidOperationException(
                $"Liberação de BRL {valor.Valor:F2} resultaria em valor utilizado negativo (utilizado atual: BRL {ValorUtilizadoBrlDecimal:F2}).");
        }

        ValorUtilizadoBrlDecimal = novoUtilizado;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
