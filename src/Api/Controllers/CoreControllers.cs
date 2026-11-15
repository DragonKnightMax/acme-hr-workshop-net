using System.ComponentModel.DataAnnotations;
using Api.Auth;
using Api.Data;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController, Route("health"), AllowAnonymous]
public class HealthController(AcmeDb db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var ok = await db.Database.CanConnectAsync();
        return Ok(new { status = ok ? "ok" : "error", database = ok ? "database connected" : "database NOT connected", provider = db.Database.ProviderName });
    }
}

[ApiController, Route("employees")]
public class EmployeesController(AcmeDb db, LeaveService leave, AccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.Employees.OrderBy(e => e.Id).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        await db.Employees.FindAsync(id) is { } e ? Ok(e) : Problem(statusCode: 404, title: $"Employee {id} not found");

    [HttpGet("{id:int}/balance")]
    public async Task<IActionResult> Balance(int id)
    {
        if (!await access.CanSeeEmployeeAsync(User, id)) return Problem(statusCode: 403, title: "You can only see your own or your team's leave");
        return await leave.GetBalanceAsync(id) is { } b ? Ok(b) : Problem(statusCode: 404, title: $"Employee {id} not found");
    }
}

[ApiController, Route("leave")]
public class LeaveController(LeaveService leave, AccessService access) : ControllerBase
{
    /// <summary>Leave you may see: your own, your direct reports' (managers), everyone's (HR).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? employeeId)
    {
        if (employeeId is int id)
            return await access.CanSeeEmployeeAsync(User, id)
                ? Ok(await leave.ListAsync(id))
                : Problem(statusCode: 403, title: "You can only see your own or your team's leave");
        var visible = await access.VisibleEmployeeIdsAsync(User);
        return Ok((await leave.ListAsync(null)).Where(r => visible is null || visible.Contains(r.EmployeeId)));
    }

    /// <summary>Create leave for the signed-in employee. Any employeeId in the body is ignored.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateLeave body)
    {
        var (r, error) = await leave.CreateAsync(User.EmployeeId(), body.StartDate!.Value, body.EndDate!.Value, body.HalfDay, body.Reason);
        return r is null ? Problem(statusCode: 400, title: error) : Created($"/leave/{r.Id}", r);
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id) => Decide(id, approve: true);

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id) => Decide(id, approve: false);

    async Task<IActionResult> Decide(int id, bool approve)
    {
        if (await leave.FindAsync(id) is not { } r) return Problem(statusCode: 404, title: $"Leave request {id} not found");
        if (!await access.CanDecideLeaveAsync(User, r)) return Problem(statusCode: 403, title: "Only the requester's manager or HR can decide this request");
        return await leave.DecideAsync(r, approve) is { } error ? Problem(statusCode: 400, title: error) : Ok(r);
    }
}

/// <summary>Dates are nullable so a missing date fails validation (400) instead of becoming 0001-01-01.</summary>
public record CreateLeave([Required] DateOnly? StartDate, [Required] DateOnly? EndDate, Api.Models.HalfDay HalfDay = Api.Models.HalfDay.None, string? Reason = null);

[ApiController, Route("policies")]
public class PoliciesController(AcmeDb db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.Policies.OrderBy(p => p.Id).Select(p => new { p.Id, p.Title }).ToListAsync());
}
