using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public record LeaveBalance(int EmployeeId, decimal Entitlement, decimal Used, decimal Pending, decimal Available);

public class LeaveService(AcmeDb db)
{
    public static decimal DaysOf(LeaveRequest r) =>
        r.HalfDay == HalfDay.None ? WorkingDays.Count(r.StartDate, r.EndDate) : 0.5m;

    public async Task<LeaveBalance?> GetBalanceAsync(int employeeId)
    {
        var emp = await db.Employees.FindAsync(employeeId);
        if (emp is null) return null;
        var requests = await db.LeaveRequests.Where(r => r.EmployeeId == employeeId).ToListAsync();
        var used = requests.Where(r => r.Status == LeaveStatus.Approved).Sum(DaysOf);
        var pending = requests.Where(r => r.Status == LeaveStatus.Pending).Sum(DaysOf);
        return new LeaveBalance(employeeId, emp.AnnualEntitlement, used, pending, emp.AnnualEntitlement - used - pending);
    }

    public Task<List<LeaveRequest>> ListAsync(int? employeeId) =>
        db.LeaveRequests.Where(r => employeeId == null || r.EmployeeId == employeeId)
            .OrderByDescending(r => r.StartDate).ToListAsync();

    public Task<LeaveRequest?> FindAsync(int id) => db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id);

    /// <summary>Creates leave for the employee. Returns the request, or an error message when a rule is broken.</summary>
    public async Task<(LeaveRequest? Request, string? Error)> CreateAsync(int employeeId, DateOnly start, DateOnly end, HalfDay halfDay, string? reason)
    {
        if (end < start) return (null, "End date must not be before start date.");
        var r = new LeaveRequest { EmployeeId = employeeId, StartDate = start, EndDate = end, HalfDay = halfDay, Reason = reason ?? "" };

        if (halfDay != HalfDay.None)
        {
            if (start != end) return (null, "A half day must start and end on the same date.");
            if (WorkingDays.Count(start, end) == 0) return (null, "A half day must be on a working day (Monday to Friday).");
        }
        else if (WorkingDays.Count(start, end) == 0) return (null, "The dates contain no working days (Monday to Friday).");

        var days = DaysOf(r);
        var balance = await GetBalanceAsync(employeeId);
        if (balance is null) return (null, "Employee not found.");
        if (balance.Available < days) return (null, $"Not enough leave: {days} days requested, {balance.Available} available.");

        var active = await db.LeaveRequests
            .Where(x => x.EmployeeId == employeeId && x.Status != LeaveStatus.Rejected)
            .ToListAsync();
        if (active.Any(x => Overlaps(x, r))) return (null, "These dates overlap another pending or approved leave request.");

        r.Status = days <= 3 ? LeaveStatus.Approved : LeaveStatus.Pending;
        db.LeaveRequests.Add(r);
        await db.SaveChangesAsync();
        return (r, null);
    }

    /// <summary>Approves or rejects a Pending request. Returns an error message when it is not Pending. Access is checked by the caller.</summary>
    public async Task<string?> DecideAsync(LeaveRequest r, bool approve)
    {
        if (r.Status != LeaveStatus.Pending) return $"Only pending requests can be decided; this one is {r.Status}.";
        r.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        await db.SaveChangesAsync();
        return null;
    }

    // An AM and a PM half day on the same date do not overlap; anything else sharing a date does.
    static bool Overlaps(LeaveRequest a, LeaveRequest b) =>
        a.StartDate <= b.EndDate && b.StartDate <= a.EndDate &&
        !(a.HalfDay != HalfDay.None && b.HalfDay != HalfDay.None && a.HalfDay != b.HalfDay);
}
