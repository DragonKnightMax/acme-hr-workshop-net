using Api.Data;

namespace Api.Services;

public static class HolidayService
{
    public static readonly string[] States = ["ALL", "SEL", "KUL", "PNG"];

    /// <summary>National holidays plus the state's own, sorted by date. Null when the state is unknown.</summary>
    public static IReadOnlyList<Holiday>? For(string state, int year)
    {
        var s = state.ToUpperInvariant();
        if (!States.Contains(s)) return null;
        return Holidays.All.Where(h => h.Date.Year == year && (h.State == "ALL" || h.State == s)).OrderBy(h => h.Date).ToList();
    }
}
