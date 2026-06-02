using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Requisito de garantia exigido pelo banco para liberar uma linha de crédito.
/// Child entity owned by <see cref="GarantiaExigidaRevisao"/> — uma revisão pode
/// ter zero, uma ou várias garantias exigidas.
///
/// AD-4 (relaxada): para tipos diferentes de Aval, exatamente um entre
/// <see cref="PercentualSobreLimite"/> ou <see cref="ValorFixoBrl"/> deve ser informado.
/// Para Aval, ambos podem ser nulos (representa exigência implícita de aval pelos sócios
/// cobrindo 100% da exposição da linha).
///
/// Imutabilidade: a invariante SR-05 (item imutável após revisão encerrada) é verificada
/// no agregado pai <see cref="GarantiaExigidaRevisao"/>, que rejeita toda tentativa de
/// modificação quando VigenciaFim != null.
/// </summary>
public sealed class GarantiaExigidaItem : Entity, IAuditable
{
    /// <summary>FK → garantia_exigida_revisao.id (substitui o antigo LimiteBancoId). SPEC §3.4.</summary>
    public Guid RevisaoId { get; private set; }

    public TipoGarantia Tipo { get; private set; }

    /// <summary>Percentual sobre o limite, em valor humano (ex: 20 = 20%). Exclusivo com ValorFixoBrl.</summary>
    public decimal? PercentualSobreLimite { get; private set; }

    internal decimal? ValorFixoBrlDecimal { get; private set; }

    /// <summary>Valor fixo em BRL exigido como garantia. Exclusivo com PercentualSobreLimite.</summary>
    public Money? ValorFixoBrl =>
        ValorFixoBrlDecimal.HasValue ? new Money(ValorFixoBrlDecimal.Value, Moeda.Brl) : null;

    public bool Obrigatoria { get; private set; }
    public string? Observacoes { get; private set; }

    /// <summary>
    /// Identificador do grupo de alternativas "OU" a que este item pertence. Null = item
    /// independente (comportamento legado, GA-01). Itens com o mesmo valor formam um grupo
    /// cuja exigência é satisfeita por uma alternativa OU pela combinação (SPEC §3, GA-02/03/07).
    /// </summary>
    public Guid? GrupoAlternativaId { get; private set; }

    /// <summary>Rótulo opcional do grupo (≤ 120 chars, GA-05). Ex.: "Colateral mínimo FINIMP".</summary>
    public string? GrupoRotulo { get; private set; }

    /// <summary>Tamanho máximo de <see cref="GrupoRotulo"/> (GA-05).</summary>
    public const int MaxGrupoRotuloLength = 120;

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private GarantiaExigidaItem() { }

    /// <summary>
    /// Cria um item a partir de um <see cref="Instant"/> explícito. Chamado por
    /// <see cref="GarantiaExigidaRevisao"/> que já capturou o momento do clock.
    /// </summary>
    internal static GarantiaExigidaItem Criar(
        Guid revisaoId,
        TipoGarantia tipo,
        decimal? percentualSobreLimite,
        Money? valorFixoBrl,
        bool obrigatoria,
        string? observacoes,
        Instant momento,
        Guid? grupoAlternativaId = null,
        string? grupoRotulo = null)
    {
        if (revisaoId == Guid.Empty)
        {
            throw new ArgumentException("RevisaoId não pode ser vazio.", nameof(revisaoId));
        }

        ValidarCamposExclusivos(tipo, percentualSobreLimite, valorFixoBrl);
        ValidarGrupo(grupoAlternativaId, grupoRotulo);

        return new GarantiaExigidaItem
        {
            RevisaoId = revisaoId,
            Tipo = tipo,
            PercentualSobreLimite = percentualSobreLimite,
            ValorFixoBrlDecimal = valorFixoBrl?.Valor,
            // GA-04: item agrupado é sempre obrigatório; o flag por item não decide enforcement.
            Obrigatoria = grupoAlternativaId.HasValue || obrigatoria,
            Observacoes = observacoes,
            GrupoAlternativaId = grupoAlternativaId,
            // GA-01: rótulo só faz sentido com grupo; sem grupo é normalizado para null.
            GrupoRotulo = grupoAlternativaId.HasValue ? grupoRotulo : null,
            CreatedAt = momento,
            UpdatedAt = momento,
        };
    }

    /// <summary>
    /// Sobrecarga que aceita IClock. Mantida para compatibilidade de testes e
    /// chamadas externas que não têm um Instant pré-capturado.
    /// </summary>
    internal static GarantiaExigidaItem Criar(
        Guid revisaoId,
        TipoGarantia tipo,
        decimal? percentualSobreLimite,
        Money? valorFixoBrl,
        bool obrigatoria,
        string? observacoes,
        IClock clock,
        Guid? grupoAlternativaId = null,
        string? grupoRotulo = null)
        => Criar(revisaoId, tipo, percentualSobreLimite, valorFixoBrl, obrigatoria, observacoes,
            clock.GetCurrentInstant(), grupoAlternativaId, grupoRotulo);

    internal void Atualizar(
        decimal? percentualSobreLimite,
        Money? valorFixoBrl,
        bool obrigatoria,
        string? observacoes,
        IClock clock,
        Guid? grupoAlternativaId = null,
        string? grupoRotulo = null)
    {
        ValidarCamposExclusivos(Tipo, percentualSobreLimite, valorFixoBrl);
        ValidarGrupo(grupoAlternativaId, grupoRotulo);

        PercentualSobreLimite = percentualSobreLimite;
        ValorFixoBrlDecimal = valorFixoBrl?.Valor;
        Obrigatoria = grupoAlternativaId.HasValue || obrigatoria; // GA-04
        Observacoes = observacoes;
        GrupoAlternativaId = grupoAlternativaId;
        GrupoRotulo = grupoAlternativaId.HasValue ? grupoRotulo : null; // GA-01
        UpdatedAt = clock.GetCurrentInstant();
    }

    private static void ValidarGrupo(Guid? grupoAlternativaId, string? grupoRotulo)
    {
        if (grupoAlternativaId == Guid.Empty)
        {
            throw new ArgumentException(
                "GrupoAlternativaId não pode ser Guid.Empty; use null para item independente.",
                nameof(grupoAlternativaId));
        }

        // GA-05 (parte item): comprimento do rótulo. A consistência entre itens do mesmo
        // grupo é validada no agregado GarantiaExigidaRevisao.
        if (grupoRotulo is not null && grupoRotulo.Length > MaxGrupoRotuloLength)
        {
            throw new ArgumentException(
                $"GrupoRotulo deve ter no máximo {MaxGrupoRotuloLength} caracteres.",
                nameof(grupoRotulo));
        }
    }

    private static void ValidarCamposExclusivos(
        TipoGarantia tipo,
        decimal? percentualSobreLimite,
        Money? valorFixoBrl)
    {
        bool temPercentual = percentualSobreLimite.HasValue;
        bool temValorFixo = valorFixoBrl.HasValue;

        if (temPercentual && temValorFixo)
        {
            throw new ArgumentException(
                "Campos percentual e valor fixo são mutuamente exclusivos — informe apenas um.",
                nameof(percentualSobreLimite));
        }

        if (!temPercentual && !temValorFixo && tipo != TipoGarantia.Aval)
        {
            throw new ArgumentException(
                "Informe percentual ou valor fixo (exceto para garantias do tipo Aval).",
                nameof(percentualSobreLimite));
        }

        if (temPercentual && (percentualSobreLimite!.Value <= 0m || percentualSobreLimite.Value > 100m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentualSobreLimite),
                "Percentual deve estar no intervalo (0, 100].");
        }

        if (temValorFixo)
        {
            if (valorFixoBrl!.Value.Moeda != Moeda.Brl)
            {
                throw new ArgumentException("ValorFixoBrl deve ser em BRL.", nameof(valorFixoBrl));
            }

            if (valorFixoBrl.Value.Valor <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(valorFixoBrl),
                    "ValorFixoBrl deve ser positivo.");
            }
        }
    }
}
