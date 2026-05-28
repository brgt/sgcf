using NodaTime;

namespace Sgcf.Application.Common;

internal static class DateOnlyExtensions
{
    internal static LocalDate ToLocalDate(this DateOnly d) => new(d.Year, d.Month, d.Day);
}
