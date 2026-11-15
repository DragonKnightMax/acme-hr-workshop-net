using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController, Route("holidays")]
public class HolidaysController : ControllerBase
{
    /// <summary>GET /holidays?state=SEL&amp;year=2026. state: ALL (national only), SEL, KUL, PNG.</summary>
    [HttpGet]
    public IActionResult Get([FromQuery] string? state, [FromQuery] int? year)
    {
        if (string.IsNullOrWhiteSpace(state) || year is null)
            return Problem(statusCode: 400, title: "state and year are required");
        return HolidayService.For(state, year.Value) is { } list
            ? Ok(list)
            : Problem(statusCode: 400, title: $"Unknown state '{state}'. Use one of: {string.Join(", ", HolidayService.States)}");
    }
}
