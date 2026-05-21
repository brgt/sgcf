using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Agregado raiz do módulo de Cotações.
/// Representa um pedido interno de captação enviado a um ou mais bancos.
/// SPEC §3.1, §3.2, §4.
/// </summary>
public sealed class Cotacao : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>Código legível por humanos (ex: COT-2026-00001).</summary>
    public string CodigoInterno { get; private set; } = default!;

    public ModalidadeContrato Modalidade { get; private set; }

    internal decimal ValorAlvoBrlDecimal { get; private set; }

    /// <summary>Valor desejado de captação em moeda funcional (BRL).</summary>
    public Money ValorAlvoBrl => new(ValorAlvoBrlDecimal, Moeda.Brl);

    public int PrazoMaximoDias { get; private set; }
    public LocalDate DataAbertura { get; private set; }

    /// <summary>
    /// Data de referência da PTAX usada. Null para modalidades BRL puras (NCE, CapitalDeGiro, FGI).
    /// Onda 0 F0.1.
    /// </summary>
    public LocalDate? DataPtaxReferencia { get; private set; }

    /// <summary>
    /// PTAX D-1 usada como referência na conversão USD/BRL para cálculo de CET.
    /// Null para modalidades BRL puras (NCE, CapitalDeGiro, FGI). Onda 0 F0.1.
    /// </summary>
    public decimal? PtaxUsadaUsdBrl { get; private set; }

    public StatusCotacao Status { get; private set; }

    /// <summary>Id da Proposta aceita, preenchido na transição para Aceita.</summary>
    public Guid? PropostaAceitaId { get; private set; }

    /// <summary>Id do Contrato gerado na conversão. Preenchido em ConverterEmContrato.</summary>
    public Guid? ContratoGeradoId { get; private set; }

    /// <summary>
    /// Id do contrato mãe imediato. Obrigatório para modalidade Refinimp; nulo nas demais.
    /// Capturado no momento da criação da cotação (rascunho) — permite validar moeda
    /// da proposta e calcular ancestral para a regra 70% BB desde o início do fluxo.
    /// SPEC §3.1 — Onda 1 REFINIMP.
    /// </summary>
    public Guid? ContratoMaeId { get; private set; }

    /// <summary>Subject (sub do JWT) do operador que aceitou a proposta. Obrigatório quando Status >= Aceita.</summary>
    public string? AceitaPor { get; private set; }

    public Instant? DataAceitacao { get; private set; }
    public string? Observacoes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Instant? DeletedAt { get; private set; }

    // Bancos-alvo adicionados à cotação (sem proposta ainda)
    private readonly List<Guid> _bancosAlvo = [];

    /// <summary>Lista de bancos convidados para esta cotação.</summary>
    public IReadOnlyCollection<Guid> BancosAlvo => _bancosAlvo.AsReadOnly();

    private readonly List<Proposta> _propostas = [];

    /// <summary>Propostas registradas pelos bancos convidados.</summary>
    public IReadOnlyCollection<Proposta> Propostas => _propostas.AsReadOnly();

    /// <summary>Construtor privado para EF Core.</summary>
    private Cotacao() { }

    /// <summary>
    /// Retorna true para modalidades que operam em moeda estrangeira e, portanto, exigem PTAX.
    /// Onda 0 F0.1 — SPEC docs/specs/cotacoes/modalidades/onda-0.md §3.2.
    /// </summary>
    /// <remarks>
    /// Modalidades cambiais: FINIMP, REFINIMP, Lei 4131.
    /// Modalidades BRL puras (NCE, CapitalDeGiro, FGI): PTAX não se aplica.
    /// </remarks>
    public static bool ExigeMoedaEstrangeira(ModalidadeContrato modalidade) =>
        modalidade == ModalidadeContrato.Finimp
        || modalidade == ModalidadeContrato.Refinimp
        || modalidade == ModalidadeContrato.Lei4131;

    /// <summary>
    /// Cria nova cotação em estado Rascunho.
    /// SPEC §3.2 — valida invariantes de criação.
    /// Onda 0 F0.1: <paramref name="ptaxUsadaUsdBrl"/> e <paramref name="dataPtaxReferencia"/>
    /// são null para modalidades BRL puras; obrigatórios para modalidades cambiais.
    /// Onda 1: <paramref name="contratoMaeId"/> é obrigatório quando <paramref name="modalidade"/> = Refinimp;
    /// proibido nas demais modalidades — SPEC §3.1 AD-1.
    /// </summary>
    public static Cotacao Criar(
        string codigoInterno,
        ModalidadeContrato modalidade,
        Money valorAlvoBrl,
        int prazoMaximoDias,
        LocalDate dataAbertura,
        LocalDate? dataPtaxReferencia,
        decimal? ptaxUsadaUsdBrl,
        IClock clock,
        string? observacoes = null,
        Guid? contratoMaeId = null)
    {
        if (string.IsNullOrWhiteSpace(codigoInterno))
        {
            throw new ArgumentException("CodigoInterno não pode ser vazio.", nameof(codigoInterno));
        }

        if (valorAlvoBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorAlvoBrl deve ser em BRL.", nameof(valorAlvoBrl));
        }

        if (valorAlvoBrl.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valorAlvoBrl), "ValorAlvoBRL deve ser maior que zero (SPEC §3.2).");
        }

        if (prazoMaximoDias < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(prazoMaximoDias), "PrazoMaximoDias deve ser maior ou igual a 1 (SPEC §3.2).");
        }

        // Onda 0 F0.1: modalidades cambiais exigem PTAX; modalidades BRL puras rejeitam PTAX.
        if (ExigeMoedaEstrangeira(modalidade) && ptaxUsadaUsdBrl is null)
        {
            throw new ArgumentException(
                $"PTAX D-1 é obrigatória para modalidade {modalidade} (operação cambial). Onda 0 F0.1.",
                nameof(ptaxUsadaUsdBrl));
        }

        if (!ExigeMoedaEstrangeira(modalidade) && ptaxUsadaUsdBrl is not null)
        {
            throw new ArgumentException(
                $"PTAX não se aplica à modalidade {modalidade} (operação em BRL). Onda 0 F0.1.",
                nameof(ptaxUsadaUsdBrl));
        }

        if (ptaxUsadaUsdBrl is not null && ptaxUsadaUsdBrl <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ptaxUsadaUsdBrl), "PtaxUsadaUsdBrl deve ser positiva.");
        }

        // SPEC §3.2 regra 7: DataPtaxReferencia deve ser anterior a DataAbertura (apenas quando fornecida).
        // Dia útil anterior é validado externamente (Application), aqui validamos apenas a anterioridade.
        if (dataPtaxReferencia is not null && dataPtaxReferencia >= dataAbertura)
        {
            throw new ArgumentException(
                "DataPtaxReferencia deve ser anterior à DataAbertura (SPEC §3.2).",
                nameof(dataPtaxReferencia));
        }

        // Onda 1 — SPEC §3.1 AD-1: REFINIMP exige ContratoMaeId; outras modalidades proíbem.
        if (modalidade == ModalidadeContrato.Refinimp &&
            (contratoMaeId is null || contratoMaeId == Guid.Empty))
        {
            throw new ArgumentException(
                "ContratoMaeId é obrigatório para cotação da modalidade Refinimp.",
                nameof(contratoMaeId));
        }

        if (modalidade != ModalidadeContrato.Refinimp && contratoMaeId is not null)
        {
            throw new ArgumentException(
                $"ContratoMaeId não se aplica à modalidade {modalidade}.",
                nameof(contratoMaeId));
        }

        var now = clock.GetCurrentInstant();
        return new Cotacao
        {
            CodigoInterno = codigoInterno,
            Modalidade = modalidade,
            ValorAlvoBrlDecimal = valorAlvoBrl.Valor,
            PrazoMaximoDias = prazoMaximoDias,
            DataAbertura = dataAbertura,
            DataPtaxReferencia = dataPtaxReferencia,
            PtaxUsadaUsdBrl = ptaxUsadaUsdBrl,
            ContratoMaeId = contratoMaeId,
            Status = StatusCotacao.Rascunho,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ─── Gestão de bancos-alvo ───────────────────────────────────────────────

    /// <summary>
    /// Adiciona banco à lista de convidados da cotação.
    /// Apenas em Rascunho, EmCaptacao, EmAnaliseBanco ou PropostaRecebida.
    /// Validação de limite disponível é responsabilidade da Application (SPEC §3.2 regra 8).
    /// </summary>
    public void AdicionarBancoAlvo(Guid bancoId)
    {
        if (Status is not StatusCotacao.Rascunho
            and not StatusCotacao.EmCaptacao
            and not StatusCotacao.EmAnaliseBanco
            and not StatusCotacao.PropostaRecebida)
        {
            throw new InvalidOperationException(
                $"Não é possível adicionar banco em cotação com status '{Status}'.");
        }

        if (!_bancosAlvo.Contains(bancoId))
        {
            _bancosAlvo.Add(bancoId);
        }
    }

    /// <summary>Remove banco da lista de convidados. Apenas em Rascunho, EmCaptacao, EmAnaliseBanco ou PropostaRecebida.</summary>
    public void RemoverBancoAlvo(Guid bancoId)
    {
        if (Status is not StatusCotacao.Rascunho
            and not StatusCotacao.EmCaptacao
            and not StatusCotacao.EmAnaliseBanco
            and not StatusCotacao.PropostaRecebida)
        {
            throw new InvalidOperationException(
                $"Não é possível remover banco em cotação com status '{Status}'.");
        }

        _bancosAlvo.Remove(bancoId);
    }

    // ─── Gestão de propostas ─────────────────────────────────────────────────

    /// <summary>
    /// Cria e registra proposta recebida de um banco. Apenas em EmCaptacao ou Comparada.
    /// CET será calculado pela Application após a criação via <see cref="AtualizarCetProposta"/>.
    /// Retorna a proposta criada para que a Application possa calcular e setar o CET.
    /// SPEC §3.3, §5.1.
    /// </summary>
    public Proposta AdicionarProposta(
        Guid bancoId,
        Moeda moedaOriginal,
        Money valorOferecidoMoedaOriginal,
        decimal taxaAaPercentual,
        decimal iofPercentual,
        decimal spreadAaPercentual,
        int prazoDias,
        EstruturaAmortizacao estruturaAmortizacao,
        Periodicidade periodicidadeJuros,
        bool exigeNdf,
        decimal? custoNdfAaPercentual,
        string garantiaExigida,
        Money valorGarantiaExigidaBrl,
        bool garantiaEhCdbCativo,
        decimal? rendimentoCdbAaPercentual,
        LocalDate dataCaptura,
        LocalDate? dataValidadeMercado = null)
    {
        if (Status is not StatusCotacao.EmCaptacao
            and not StatusCotacao.EmAnaliseBanco
            and not StatusCotacao.PropostaRecebida
            and not StatusCotacao.Comparada)
        {
            throw new InvalidOperationException(
                $"Não é possível registrar proposta em cotação com status '{Status}'.");
        }

        Proposta proposta = new(
            Id,
            bancoId,
            moedaOriginal,
            valorOferecidoMoedaOriginal,
            taxaAaPercentual,
            iofPercentual,
            spreadAaPercentual,
            prazoDias,
            estruturaAmortizacao,
            periodicidadeJuros,
            exigeNdf,
            custoNdfAaPercentual,
            garantiaExigida,
            valorGarantiaExigidaBrl,
            garantiaEhCdbCativo,
            rendimentoCdbAaPercentual,
            dataCaptura,
            dataValidadeMercado);

        _propostas.Add(proposta);
        return proposta;
    }

    /// <summary>
    /// Atualiza cache de CET e valor total de uma proposta existente.
    /// Chamado pela Application após calcular o CET via CalculadoraCet.
    /// </summary>
    public void AtualizarCetProposta(Guid propostaId, decimal cetAaPercentual, Money valorTotalEstimadoBrl)
    {
        Proposta proposta = EncontrarPropostaPorId(propostaId);
        proposta.AtualizarCacheCalculos(cetAaPercentual, valorTotalEstimadoBrl);
    }

    /// <summary>
    /// Registra proposta recebida de um banco. Apenas em EmCaptacao, EmAnaliseBanco, PropostaRecebida ou Comparada.
    /// Invalida cache de CET na proposta após registro.
    /// </summary>
    internal void RegistrarProposta(Proposta proposta)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        if (Status is not StatusCotacao.EmCaptacao
            and not StatusCotacao.EmAnaliseBanco
            and not StatusCotacao.PropostaRecebida
            and not StatusCotacao.Comparada)
        {
            throw new InvalidOperationException(
                $"Não é possível registrar proposta em cotação com status '{Status}'.");
        }

        if (proposta.CotacaoId != Id)
        {
            throw new ArgumentException(
                "CotacaoId da proposta não corresponde a esta cotação.", nameof(proposta));
        }

        _propostas.Add(proposta);
    }

    // ─── Transições de estado ────────────────────────────────────────────────

    /// <summary>
    /// Transição: Rascunho → EmCaptacao.
    /// SPEC §4.1.
    /// </summary>
    public void Enviar(IClock clock)
    {
        ExigirStatus(StatusCotacao.Rascunho, nameof(Enviar));
        Status = StatusCotacao.EmCaptacao;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Transição: EmCaptacao | EmAnaliseBanco | PropostaRecebida → Comparada.
    /// Chamado manualmente pelo operador quando encerra o período de captação.
    /// SPEC §4.1.
    /// </summary>
    public void EncerrarCaptacao(IClock clock)
    {
        if (Status is not StatusCotacao.EmCaptacao
            and not StatusCotacao.EmAnaliseBanco
            and not StatusCotacao.PropostaRecebida)
        {
            throw new InvalidOperationException(
                $"Operação '{nameof(EncerrarCaptacao)}' requer status 'EmCaptacao', 'EmAnaliseBanco' ou 'PropostaRecebida', mas status atual é '{Status}'.");
        }

        Status = StatusCotacao.Comparada;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Transição: EmCaptacao → EmAnaliseBanco.
    /// Banco confirmou o recebimento da cotação e está analisando internamente.
    /// SPEC §4.1.
    /// </summary>
    public void RegistrarAnalise(IClock clock)
    {
        ExigirStatus(StatusCotacao.EmCaptacao, nameof(RegistrarAnalise));
        Status = StatusCotacao.EmAnaliseBanco;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Transição: EmAnaliseBanco → PropostaRecebida.
    /// Deve ser chamado quando a primeira <see cref="Proposta"/> é registrada e a cotação
    /// ainda está em <see cref="StatusCotacao.EmAnaliseBanco"/>.
    /// Se a cotação já está em <see cref="StatusCotacao.PropostaRecebida"/> ou
    /// <see cref="StatusCotacao.Comparada"/>, o método é no-op (idempotente).
    /// SPEC §4.1.
    /// </summary>
    public void RegistrarPrimeiraPropostaRecebida(IClock clock)
    {
        // No-op quando já avançou além deste ponto
        if (Status is StatusCotacao.PropostaRecebida or StatusCotacao.Comparada)
        {
            return;
        }

        if (Status is not StatusCotacao.EmAnaliseBanco and not StatusCotacao.EmCaptacao)
        {
            throw new InvalidOperationException(
                $"Operação '{nameof(RegistrarPrimeiraPropostaRecebida)}' requer status 'EmAnaliseBanco' ou 'EmCaptacao', mas status atual é '{Status}'.");
        }

        Status = StatusCotacao.PropostaRecebida;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Transição: Comparada → Aceita.
    /// Define proposta aceita e registra auditor (AceitaPor = sub do JWT).
    /// SPEC §4.1, §3.2 regras 4 e 6.
    /// </summary>
    public void AceitarProposta(Guid propostaId, string actorSub, IClock clock)
    {
        ExigirStatus(StatusCotacao.Comparada, nameof(AceitarProposta));

        if (string.IsNullOrWhiteSpace(actorSub))
        {
            throw new ArgumentException("ActorSub (AceitaPor) é obrigatório para aceitar proposta.", nameof(actorSub));
        }

        Proposta proposta = EncontrarPropostaPorId(propostaId);

        // Garante invariante: apenas uma proposta aceita por cotação
        if (_propostas.Any(p => p.Status == StatusProposta.Aceita))
        {
            throw new InvalidOperationException("Já existe uma proposta aceita para esta cotação (SPEC §3.2).");
        }

        proposta.Aceitar();

        PropostaAceitaId = propostaId;
        AceitaPor = actorSub;
        DataAceitacao = clock.GetCurrentInstant();
        Status = StatusCotacao.Aceita;
        UpdatedAt = DataAceitacao.Value;
    }

    /// <summary>
    /// Transição: Aceita → Comparada.
    /// Apenas se ainda não convertida. Remove marcação de proposta aceita.
    /// SPEC §4.1.
    /// </summary>
    public void DesfazerAceitacao(IClock clock)
    {
        ExigirStatus(StatusCotacao.Aceita, nameof(DesfazerAceitacao));

        Proposta propostaAtual = EncontrarPropostaPorId(PropostaAceitaId!.Value);
        propostaAtual.ReverterAceitacao();

        PropostaAceitaId = null;
        AceitaPor = null;
        DataAceitacao = null;
        Status = StatusCotacao.Comparada;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Transição: Aceita → Convertida.
    /// Registra ContratoGeradoId. EconomiaNegociacao é criada pela Application.
    /// SPEC §4.1, §3.2 regra 5.
    /// </summary>
    public void ConverterEmContrato(Guid contratoId, IClock clock)
    {
        ExigirStatus(StatusCotacao.Aceita, nameof(ConverterEmContrato));

        if (!PropostaAceitaId.HasValue)
        {
            throw new InvalidOperationException(
                "Não há proposta aceita para converter em contrato (SPEC §3.2 regra 5).");
        }

        ContratoGeradoId = contratoId;
        Status = StatusCotacao.Convertida;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Transição: Rascunho | EmCaptacao | EmAnaliseBanco | PropostaRecebida | Comparada → Recusada.
    /// SPEC §4.1. Status finais (Convertida, Recusada) não têm saída.
    /// </summary>
    public void Cancelar(string motivo, IClock clock)
    {
        if (Status is StatusCotacao.Convertida or StatusCotacao.Recusada)
        {
            throw new InvalidOperationException(
                $"Cotação com status '{Status}' é um estado final e não pode ser cancelada.");
        }

        // Aceita também pode ser cancelada — não está na SPEC mas é defensivo.
        // Decisão de design: evitar buraco na FSM. Registrado no relatório final.
        Observacoes = string.IsNullOrWhiteSpace(motivo)
            ? Observacoes
            : $"[CANCELAMENTO] {motivo}";

        Status = StatusCotacao.Recusada;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Atualiza snapshot de mercado (PTAX e invalida CET de todas as propostas).
    /// Permite uso em cotações em estado EmCaptacao ou Comparada.
    /// SPEC §6.1 RefreshCotacaoMercadoCommand.
    /// </summary>
    public void RefreshSnapshotMercado(decimal novoPtax, IClock clock)
    {
        if (Status is not StatusCotacao.EmCaptacao
            and not StatusCotacao.EmAnaliseBanco
            and not StatusCotacao.PropostaRecebida
            and not StatusCotacao.Comparada)
        {
            throw new InvalidOperationException(
                $"RefreshSnapshotMercado só é permitido em EmCaptacao, EmAnaliseBanco, PropostaRecebida ou Comparada. Status atual: '{Status}'.");
        }

        if (novoPtax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(novoPtax), "Nova PTAX deve ser positiva.");
        }

        PtaxUsadaUsdBrl = novoPtax;
        UpdatedAt = clock.GetCurrentInstant();

        // Invalida cache de CET em todas as propostas — devem ser recalculados
        foreach (Proposta p in _propostas)
        {
            p.InvalidarCacheCalculos();
        }
    }

    /// <summary>
    /// Edita campos básicos da cotação (PrazoMaximoDias e/ou Observacoes).
    /// Permitido apenas em Rascunho para garantir integridade antes do envio. SPEC §7.1.
    /// </summary>
    public void EditarCamposBasicos(int? prazoMaximoDias, string? observacoes, IClock clock)
    {
        ExigirStatus(StatusCotacao.Rascunho, nameof(EditarCamposBasicos));

        if (prazoMaximoDias.HasValue)
        {
            if (prazoMaximoDias.Value < 1)
            {
                throw new ArgumentException("PrazoMaximoDias deve ser maior ou igual a 1.");
            }

            PrazoMaximoDias = prazoMaximoDias.Value;
        }

        if (observacoes is not null)
        {
            Observacoes = observacoes;
        }

        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>Soft delete. Apenas em Rascunho (SPEC §12.3).</summary>
    public void Deletar(IClock clock)
    {
        if (Status != StatusCotacao.Rascunho)
        {
            throw new InvalidOperationException(
                "Apenas cotações em Rascunho podem ser excluídas (SPEC §12.3).");
        }

        DeletedAt = clock.GetCurrentInstant();
        UpdatedAt = DeletedAt.Value;
    }

    // ─── Helpers privados ────────────────────────────────────────────────────

    private void ExigirStatus(StatusCotacao statusEsperado, string operacao)
    {
        if (Status != statusEsperado)
        {
            throw new InvalidOperationException(
                $"Operação '{operacao}' requer status '{statusEsperado}', mas status atual é '{Status}'.");
        }
    }

    private Proposta EncontrarPropostaPorId(Guid propostaId)
    {
        return _propostas.FirstOrDefault(p => p.Id == propostaId)
            ?? throw new InvalidOperationException($"Proposta '{propostaId}' não encontrada nesta cotação.");
    }
}
