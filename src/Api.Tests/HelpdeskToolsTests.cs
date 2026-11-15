using System.Text.Json;
using Api.Helpdesk;
using Api.Services;
using Xunit;

namespace Api.Tests;

public class HelpdeskToolsTests
{
    [Fact]
    public async Task Any_tool_failure_comes_back_as_an_error_result_not_an_exception()
    {
        var db = TestDb.Create();
        var tools = new HelpdeskTools(db, new LeaveService(db));
        var input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{"start_date": 5, "end_date": 6}""")!;
        var outcome = await tools.RunAsync("apply_leave", input, 6);
        Assert.True(outcome.IsError);
    }

    static async Task<(JsonElement Result, Api.Data.AcmeDb Db)> Apply(string start, string end)
    {
        var db = TestDb.Create();
        var tools = new HelpdeskTools(db, new LeaveService(db));
        var input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""{"start_date":"{{start}}","end_date":"{{end}}"}""")!;
        var outcome = await tools.RunAsync("apply_leave", input, 1);
        Assert.False(outcome.IsError, outcome.Json);
        return (JsonDocument.Parse(outcome.Json).RootElement.Clone(), db);
    }

    [Fact]
    public async Task Three_working_days_are_approved()
    {
        var (r, _) = await Apply("2026-11-16", "2026-11-18"); // Mon-Wed
        Assert.Equal("approved", r.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Four_working_days_need_manager_approval_and_stay_pending()
    {
        var (r, db) = await Apply("2026-11-16", "2026-11-19"); // Mon-Thu
        Assert.Equal("needs_manager_approval", r.GetProperty("status").GetString());
        var saved = await db.LeaveRequests.FindAsync(r.GetProperty("request_id").GetInt32());
        Assert.Equal(Api.Models.LeaveStatus.Pending, saved!.Status);
    }

    [Fact]
    public async Task Weekend_days_do_not_count_toward_the_limit()
    {
        var (r, _) = await Apply("2026-11-19", "2026-11-23"); // Thu-Mon = 3 working days
        Assert.Equal("approved", r.GetProperty("status").GetString());
    }
}
