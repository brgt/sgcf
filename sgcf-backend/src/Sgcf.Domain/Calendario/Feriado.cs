using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Calendario;

/// <summary>
/// Registro persistido de um feriado, utilizado pelo motor de cronograma para
/// determinar dias úteis. Atende a Pergunta 2 do PO sobre ajuste automático
/// de vencimentos que recaiam em dia não útil.
/// </summary>
public sealed class Feriado : Entity
{
    private const int DescricaoMaxLength = 120;

    public LocalDate Data { get; private set; }
    public TipoFeriado Tipo { get; private set; }
    public EscopoFeriado Escopo { get; private set; }
    public string Descricao { get; private set; } = default!;
    public FonteFeriado Fonte { get; private set; }
    public int AnoReferencia { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private Feriado() { }

    public static Feriado Criar(
        LocalDate data,
        TipoFeriado tipo,
        EscopoFeriado escopo,
        string descricao,
        FonteFeriado fonte,
        int anoReferencia,
        IClock clock)
    {
        ValidarDescricao(descricao);

        if (anoReferencia != data.Year)
        {
            throw new ArgumentException(
                $"AnoReferencia ({anoReferencia}) deve coincidir com o ano da data ({data.Year}).",
                nameof(anoReferencia));
        }

        Instant now = clock.GetCurrentInstant();
        return new Feriado
        {
            Data = data,
            Tipo = tipo,
            Escopo = escopo,
            Descricao = descricao.Trim(),
            Fonte = fonte,
            AnoReferencia = anoReferencia,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AtualizarDescricao(string novaDescricao, IClock clock)
    {
        ValidarDescricao(novaDescricao);
        Descricao = novaDescricao.Trim();
        UpdatedAt = clock.GetCurrentInstant();
    }

    private static void ValidarDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descricao não pode ser vazia.", nameof(descricao));
        }

        if (descricao.Length > DescricaoMaxLength)
        {
            throw new ArgumentException(
                $"Descricao excede o limite de {DescricaoMaxLength} caracteres.",
                nameof(descricao));
        }
    }
}
