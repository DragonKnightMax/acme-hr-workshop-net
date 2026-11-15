using Anthropic.Models.Messages;
using Api.Helpdesk;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class HelpdeskAgentTests
{
    static (HelpdeskAgent agent, FakeClaude fake) Make(params string[] replies) => Make(TestDb.Create(), replies);

    static (HelpdeskAgent agent, FakeClaude fake) Make(Api.Data.AcmeDb db, params string[] replies)
    {
        var fake = new FakeClaude(replies);
        return (new HelpdeskAgent(fake, new HelpdeskTools(db, new LeaveService(db)), new ClaudeOptions("claude-sonnet-5-5")), fake);
    }

    static readonly List<ChatTurn> Ask = [new("user", "How many days do I have?")];

    [Fact]
    public async Task Plain_answer_ends_after_one_turn()
    {
        var (agent, _) = Make(FakeClaude.Text("You have 14 days."));
        var r = await agent.RunAsync(1, Ask);
        Assert.Equal("You have 14 days.", r.Reply);
        Assert.Equal(1, r.Turns);
    }

    [Fact]
    public async Task Tool_call_then_answer()
    {
        var (agent, fake) = Make(FakeClaude.ToolUse("t1", "get_leave_balance", new { }), FakeClaude.Text("You have 14 days."));
        var r = await agent.RunAsync(1, Ask);
        Assert.Single(r.ToolCalls);
        Assert.Equal(2, r.Turns);
        // Second request resends history: user, assistant (tool_use), user (tool_result)
        Assert.Equal(3, fake.Requests[1].Messages.Count);
    }

    [Fact]
    public async Task Five_day_leave_needs_manager_approval_from_code()
    {
        var (agent, _) = Make(
            FakeClaude.ToolUse("t1", "apply_leave", new { start_date = "2026-11-16", end_date = "2026-11-20", half_day = "None" }),
            FakeClaude.Text("Sent to your manager."));
        var r = await agent.RunAsync(1, Ask);
        Assert.Contains("needs_manager_approval", r.ToolCalls[0].Output);
    }

    [Fact]
    public async Task Tool_error_is_returned_not_thrown()
    {
        var (agent, _) = Make(
            FakeClaude.ToolUse("t1", "apply_leave", new { start_date = "16/11/2026", end_date = "2026-11-20" }),
            FakeClaude.Text("Please use yyyy-MM-dd."));
        var r = await agent.RunAsync(1, Ask);
        Assert.True(r.ToolCalls[0].IsError);
    }

    [Fact]
    public async Task Stops_at_turn_limit()
    {
        var loop = Enumerable.Range(0, 20).Select(i => FakeClaude.ToolUse($"t{i}", "get_leave_balance", new { })).ToArray();
        var (agent, _) = Make(loop);
        var r = await agent.RunAsync(1, Ask);
        Assert.Equal(HelpdeskAgent.MaxTurns, r.Turns);
        Assert.Equal("turn_limit", r.StopReason);
    }

    [Fact]
    public async Task Each_request_is_a_snapshot_of_history_not_the_live_list()
    {
        var (agent, fake) = Make(FakeClaude.ToolUse("t1", "get_leave_balance", new { }), FakeClaude.Text("You have 14 days."));
        await agent.RunAsync(1, Ask);
        Assert.Single(fake.Requests[0].Messages);
        Assert.Equal(3, fake.Requests[1].Messages.Count);
    }

    [Fact]
    public async Task Every_tool_use_gets_its_own_tool_result_in_one_message()
    {
        var (agent, fake) = Make(
            FakeClaude.ToolUses(("t1", "get_leave_balance", new { }), ("t2", "no_such_tool", new { }), ("t3", "check_policy", new { topic = "medical" })),
            FakeClaude.Text("Done."));
        var r = await agent.RunAsync(1, Ask);
        var results = FakeClaude.ToolResults(fake.Requests[1]);
        Assert.Equal(["t1", "t2", "t3"], results.Select(x => x.ToolUseId));
        Assert.Equal([false, true, false], results.Select(x => x.IsError));
        Assert.Equal(3, r.ToolCalls.Count);
    }

    [Fact]
    public async Task Tool_that_throws_still_gets_an_error_tool_result()
    {
        var db = TestDb.Create();
        db.Database.ExecuteSqlRaw("DROP TABLE LeaveRequests"); // makes get_leave_balance throw a SqliteException
        var (agent, fake) = Make(db, FakeClaude.ToolUse("t1", "get_leave_balance", new { }), FakeClaude.Text("Sorry."));
        var r = await agent.RunAsync(1, Ask);
        var result = Assert.Single(FakeClaude.ToolResults(fake.Requests[1]));
        Assert.Equal("t1", result.ToolUseId);
        Assert.True(result.IsError);
        Assert.Equal("Sorry.", r.Reply);
    }

    [Fact]
    public async Task Non_tool_stop_reason_is_returned()
    {
        var (agent, _) = Make(FakeClaude.Text("You have", stop: "max_tokens"));
        var r = await agent.RunAsync(1, Ask);
        Assert.Equal("max_tokens", r.StopReason);
        Assert.Equal(1, r.Turns);
    }

    [Fact]
    public async Task Turn_limit_sends_exactly_max_turns_requests()
    {
        var loop = Enumerable.Range(0, 20).Select(i => FakeClaude.ToolUse($"t{i}", "get_leave_balance", new { })).ToArray();
        var (agent, fake) = Make(loop);
        var r = await agent.RunAsync(1, Ask);
        Assert.Equal(HelpdeskAgent.MaxTurns, fake.Requests.Count);
        Assert.Contains("HR officer", r.Reply);
    }
}
