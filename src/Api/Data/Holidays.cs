namespace Api.Data;

/// <summary>One public holiday. State "ALL" means national.</summary>
public record Holiday(DateOnly Date, string Name, string State);

/// <summary>Synthetic training data, not an official calendar.</summary>
public static class Holidays
{
    // shortcut: hard-coded 2026 only; move to a table if HR needs to edit holidays.
    public static readonly Holiday[] All =
    [
        new(new(2026, 1, 1), "New Year's Day", "KUL"),
        new(new(2026, 1, 1), "New Year's Day", "SEL"),
        new(new(2026, 1, 1), "New Year's Day", "PNG"),
        new(new(2026, 2, 1), "Federal Territory Day", "KUL"),
        new(new(2026, 2, 17), "Chinese New Year", "ALL"),
        new(new(2026, 2, 18), "Chinese New Year (Day 2)", "ALL"),
        new(new(2026, 3, 21), "Hari Raya Aidilfitri", "ALL"),
        new(new(2026, 5, 1), "Labour Day", "ALL"),
        new(new(2026, 6, 1), "Agong's Birthday", "ALL"),
        new(new(2026, 7, 7), "George Town World Heritage Day", "PNG"),
        new(new(2026, 7, 11), "Penang Governor's Birthday", "PNG"),
        new(new(2026, 8, 31), "Merdeka Day", "ALL"),
        new(new(2026, 9, 16), "Malaysia Day", "ALL"),
        new(new(2026, 12, 11), "Sultan of Selangor's Birthday", "SEL"),
        new(new(2026, 12, 25), "Christmas Day", "ALL"),
    ];
}
