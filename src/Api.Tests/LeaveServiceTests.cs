using Api.Models;
using Api.Services;

namespace Api.Tests;

public class LeaveServiceTests
{
    [Fact]
    public async Task Balance_for_new_employee_is_full_entitlement()
    {
        using var db = TestDb.Create();
        var b = await new LeaveService(db).GetBalanceAsync(1);
        Assert.NotNull(b);
        Assert.Equal(b!.Entitlement, b.Available);
    }

    [Fact]
    public async Task Balance_for_unknown_employee_is_null()
    {
        using var db = TestDb.Create();
        Assert.Null(await new LeaveService(db).GetBalanceAsync(999));
    }

    [Fact]
    public async Task Approved_leave_reduces_balance()
    {
        using var db = TestDb.Create();
        var b = await new LeaveService(db).GetBalanceAsync(6); // seed: 3 approved days
        Assert.Equal(3, b!.Used);
    }

    [Fact]
    public async Task Half_day_counts_half()
    {
        using var db = TestDb.Create();
        var b = await new LeaveService(db).GetBalanceAsync(7); // seed: AM half day
        Assert.Equal(0.5m, b!.Used);
    }

    [Fact]
    public async Task Pending_leave_is_held_from_available()
    {
        using var db = TestDb.Create();
        var b = await new LeaveService(db).GetBalanceAsync(8); // seed: 5 pending days
        Assert.Equal(5, b!.Pending);
        Assert.Equal(b.Entitlement - 5, b.Available);
    }

    [Fact]
    public async Task List_filters_by_employee()
    {
        using var db = TestDb.Create();
        var list = await new LeaveService(db).ListAsync(6);
        Assert.All(list, r => Assert.Equal(6, r.EmployeeId));
    }

    [Fact]
    public void Rejected_leave_days_still_computed() =>
        Assert.Equal(1, LeaveService.DaysOf(new LeaveRequest { StartDate = new(2026, 11, 18), EndDate = new(2026, 11, 18), Status = LeaveStatus.Rejected }));
}

// Dates in Jan 2027, clear of the seeded leave (which is in the current year). 4 Jan 2027 is a Monday.
public class LeaveCreateTests
{
    static readonly DateOnly Mon = new(2027, 1, 4), Tue = Mon.AddDays(1), Thu = Mon.AddDays(3), Fri = Mon.AddDays(4), Sat = Mon.AddDays(5), Sun = Mon.AddDays(6);
    const int Farah = 6; // 18 days (id divisible by 3), 3 used by the seed: 15 available

    static async Task<string?> ErrorOf(LeaveService s, int emp, DateOnly start, DateOnly end, HalfDay half = HalfDay.None) =>
        (await s.CreateAsync(emp, start, end, half, "test")).Error;

    [Fact]
    public async Task End_before_start_is_rejected()
    {
        using var db = TestDb.Create();
        Assert.NotNull(await ErrorOf(new LeaveService(db), Farah, Tue, Mon));
    }

    [Fact]
    public async Task Half_day_must_be_a_single_working_day()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        Assert.NotNull(await ErrorOf(s, Farah, Mon, Tue, HalfDay.AM));
        Assert.NotNull(await ErrorOf(s, Farah, Sat, Sat, HalfDay.PM));
    }

    [Fact]
    public async Task Weekend_only_full_day_is_rejected()
    {
        using var db = TestDb.Create();
        Assert.NotNull(await ErrorOf(new LeaveService(db), Farah, Sat, Sun));
    }

    [Fact]
    public async Task More_than_available_balance_is_rejected()
    {
        using var db = TestDb.Create();
        Assert.NotNull(await ErrorOf(new LeaveService(db), Farah, Mon, Mon.AddDays(21))); // 16 working days > 15
    }

    [Fact]
    public async Task Up_to_three_days_is_approved_more_is_pending()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        var three = await s.CreateAsync(Farah, Mon, Mon.AddDays(2), HalfDay.None, "a");
        var half = await s.CreateAsync(Farah, Thu, Thu, HalfDay.AM, "b");
        var four = await s.CreateAsync(Farah, Mon.AddDays(7), Mon.AddDays(10), HalfDay.None, "c");
        Assert.Equal(LeaveStatus.Approved, three.Request!.Status);
        Assert.Equal(LeaveStatus.Approved, half.Request!.Status);
        Assert.Equal(LeaveStatus.Pending, four.Request!.Status);
        Assert.Equal(Farah, four.Request.EmployeeId);
        Assert.Equal(4, four.Request.Days);
    }

    [Fact]
    public async Task Weekend_inside_the_range_is_not_counted()
    {
        using var db = TestDb.Create();
        var r = (await new LeaveService(db).CreateAsync(Farah, Fri, Mon.AddDays(7), HalfDay.None, "x")).Request!;
        Assert.Equal(2, r.Days);
        Assert.Equal(LeaveStatus.Approved, r.Status);
    }

    [Fact]
    public async Task Overlap_with_own_pending_or_approved_is_rejected()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        Assert.Null(await ErrorOf(s, Farah, Mon, Tue));
        Assert.NotNull(await ErrorOf(s, Farah, Tue, Thu));
        Assert.NotNull(await ErrorOf(s, Farah, Mon, Mon, HalfDay.PM));
    }

    [Fact]
    public async Task Rejected_leave_does_not_block()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        var r = (await s.CreateAsync(Farah, Mon, Thu, HalfDay.None, "x")).Request!;
        Assert.Null(await s.DecideAsync(r, approve: false));
        Assert.Null(await ErrorOf(s, Farah, Mon, Thu));
    }

    [Fact]
    public async Task AM_and_PM_on_the_same_day_do_not_overlap_but_same_half_does()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        Assert.Null(await ErrorOf(s, Farah, Mon, Mon, HalfDay.AM));
        Assert.Null(await ErrorOf(s, Farah, Mon, Mon, HalfDay.PM));
        Assert.Null(await ErrorOf(s, Farah, Tue, Tue, HalfDay.AM));
        Assert.NotNull(await ErrorOf(s, Farah, Tue, Tue, HalfDay.AM));
    }

    [Fact]
    public async Task Full_day_overlaps_a_half_day()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        Assert.Null(await ErrorOf(s, Farah, Mon, Mon, HalfDay.AM));
        Assert.NotNull(await ErrorOf(s, Farah, Mon, Tue));
    }

    [Fact]
    public async Task Other_employees_leave_does_not_overlap()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        Assert.Null(await ErrorOf(s, Farah, Mon, Tue));
        Assert.Null(await ErrorOf(s, 7, Mon, Tue));
    }

    [Fact]
    public async Task Past_start_date_is_allowed()
    {
        using var db = TestDb.Create();
        Assert.Null(await ErrorOf(new LeaveService(db), Farah, new(2024, 1, 2), new(2024, 1, 2)));
    }

    [Fact]
    public async Task Approve_and_reject_only_pending()
    {
        using var db = TestDb.Create();
        var s = new LeaveService(db);
        var pending = (await s.CreateAsync(Farah, Mon, Thu, HalfDay.None, "x")).Request!;
        Assert.Null(await s.DecideAsync(pending, approve: true));
        Assert.Equal(LeaveStatus.Approved, (await s.FindAsync(pending.Id))!.Status);
        Assert.NotNull(await s.DecideAsync(pending, approve: false));
        Assert.Equal(LeaveStatus.Approved, (await s.FindAsync(pending.Id))!.Status);
    }

    [Fact]
    public async Task Unknown_request_is_not_found()
    {
        using var db = TestDb.Create();
        Assert.Null(await new LeaveService(db).FindAsync(9999));
    }
}
