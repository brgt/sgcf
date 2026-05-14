using NodaTime;

namespace Sgcf.Domain.Calendario;

/// <summary>
/// Operações puras sobre dias úteis dado um conjunto de feriados.
/// Considera sábado e domingo como dias não úteis e o conjunto de feriados
/// fornecido como dias adicionais não úteis. Funções puras — sem I/O.
/// </summary>
public static class BusinessDayCalculator
{
    /// <summary>
    /// Retorna verdadeiro quando a data não é sábado, domingo, nem feriado.
    /// </summary>
    public static bool IsBusinessDay(LocalDate date, IReadOnlySet<LocalDate> feriados)
    {
        ArgumentNullException.ThrowIfNull(feriados);

        return date.DayOfWeek != IsoDayOfWeek.Saturday
            && date.DayOfWeek != IsoDayOfWeek.Sunday
            && !feriados.Contains(date);
    }

    /// <summary>
    /// Próximo dia útil em sequência inclusiva — se <paramref name="date"/>
    /// já é útil, retorna ela mesma.
    /// </summary>
    public static LocalDate NextBusinessDay(LocalDate date, IReadOnlySet<LocalDate> feriados)
    {
        LocalDate cursor = date;
        while (!IsBusinessDay(cursor, feriados))
        {
            cursor = cursor.PlusDays(1);
        }
        return cursor;
    }

    /// <summary>
    /// Dia útil anterior em sequência inclusiva — se <paramref name="date"/>
    /// já é útil, retorna ela mesma.
    /// </summary>
    public static LocalDate PreviousBusinessDay(LocalDate date, IReadOnlySet<LocalDate> feriados)
    {
        LocalDate cursor = date;
        while (!IsBusinessDay(cursor, feriados))
        {
            cursor = cursor.PlusDays(-1);
        }
        return cursor;
    }

    /// <summary>
    /// Avança <paramref name="n"/> dias úteis a partir de <paramref name="date"/>.
    /// Aceita valores negativos. Quando n = 0 retorna a própria data (sem ajuste).
    /// </summary>
    public static LocalDate AddBusinessDays(LocalDate date, int n, IReadOnlySet<LocalDate> feriados)
    {
        if (n == 0)
        {
            return date;
        }

        LocalDate cursor = date;
        int step = n > 0 ? 1 : -1;
        int remaining = Math.Abs(n);

        while (remaining > 0)
        {
            cursor = cursor.PlusDays(step);
            if (IsBusinessDay(cursor, feriados))
            {
                remaining--;
            }
        }

        return cursor;
    }

    /// <summary>
    /// Conta dias úteis no intervalo semiaberto [start, end). Quando
    /// start == end, retorna 0. Lança <see cref="ArgumentException"/> se
    /// start &gt; end.
    /// </summary>
    public static int CountBusinessDays(
        LocalDate start,
        LocalDate end,
        IReadOnlySet<LocalDate> feriados)
    {
        if (start > end)
        {
            throw new ArgumentException(
                $"start ({start}) não pode ser posterior a end ({end}).",
                nameof(start));
        }

        int count = 0;
        LocalDate cursor = start;
        while (cursor < end)
        {
            if (IsBusinessDay(cursor, feriados))
            {
                count++;
            }
            cursor = cursor.PlusDays(1);
        }
        return count;
    }

    /// <summary>
    /// Aplica a convenção de mercado para datas que recaiam em dia não útil
    /// (ISDA 2006 §4.12). Para <see cref="ConvencaoDataNaoUtil.Unadjusted"/>
    /// retorna a data sem ajuste.
    /// </summary>
    public static LocalDate AjustarPorConvencao(
        LocalDate date,
        ConvencaoDataNaoUtil convencao,
        IReadOnlySet<LocalDate> feriados)
    {
        if (convencao == ConvencaoDataNaoUtil.Unadjusted)
        {
            return date;
        }

        if (IsBusinessDay(date, feriados))
        {
            return date;
        }

        return convencao switch
        {
            ConvencaoDataNaoUtil.Following => NextBusinessDay(date, feriados),
            ConvencaoDataNaoUtil.Preceding => PreviousBusinessDay(date, feriados),
            ConvencaoDataNaoUtil.ModifiedFollowing => AjustarModifiedFollowing(date, feriados),
            ConvencaoDataNaoUtil.ModifiedPreceding => AjustarModifiedPreceding(date, feriados),
            _ => date
        };
    }

    private static LocalDate AjustarModifiedFollowing(
        LocalDate date,
        IReadOnlySet<LocalDate> feriados)
    {
        LocalDate next = NextBusinessDay(date, feriados);
        if (next.Month == date.Month && next.Year == date.Year)
        {
            return next;
        }
        return PreviousBusinessDay(date, feriados);
    }

    private static LocalDate AjustarModifiedPreceding(
        LocalDate date,
        IReadOnlySet<LocalDate> feriados)
    {
        LocalDate prev = PreviousBusinessDay(date, feriados);
        if (prev.Month == date.Month && prev.Year == date.Year)
        {
            return prev;
        }
        return NextBusinessDay(date, feriados);
    }
}
