using Api.Research;
using Api.Services;

namespace Api.Tests;

public class PolicyResearchTests
{
    static PolicyResearch Make(FakeClaude fake) => new(fake, TestDb.Create(), new ClaudeOptions("claude-sonnet-5-5"));

    [Fact]
    public void Validator_flags_uncited_sentence()
    {
        var bad = PolicyResearch.UncitedSentences("You can carry forward 5 days [POL-04]. Half days are fine.");
        Assert.Single(bad);
    }

    [Fact]
    public void Validator_accepts_all_cited()
    {
        Assert.Empty(PolicyResearch.UncitedSentences("You can carry forward 5 days [POL-04]. Half days deduct 0.5 [POL-03, POL-04]."));
    }

    [Fact]
    public async Task Lead_runs_a_worker_and_returns_cited_answer()
    {
        var fake = new FakeClaude(
            FakeClaude.ToolUse("l1", "research", new { subtopic = "carry forward" }),   // lead
            FakeClaude.ToolUse("w1", "search_policies", new { query = "carry" }),     // worker
            FakeClaude.Text("Up to 5 days by 31 March [POL-04]"),                      // worker done
            FakeClaude.Text("You can carry forward up to 5 days [POL-04]."));          // lead answer
        var r = await Make(fake).AskAsync("Can I carry forward leave?");
        Assert.True(r.CitationsValid);
        Assert.Contains(r.Agents, a => a.Agent == "worker-1");
        Assert.True(r.TotalTokens > 0);
    }

    [Fact]
    public async Task Worker_limit_is_enforced()
    {
        var calls = Enumerable.Range(1, 5).Select(i => FakeClaude.ToolUse($"l{i}", "research", new { subtopic = $"t{i}" })).ToList();
        var replies = new List<string>();
        foreach (var c in calls) { replies.Add(c); replies.Add(FakeClaude.Text("finding [POL-01]")); }
        var fake = new FakeClaude([.. replies, FakeClaude.Text("Done [POL-01].")]);
        var r = await Make(fake).AskAsync("Many topics");
        Assert.True(r.Agents.Count(a => a.Agent.StartsWith("worker")) <= PolicyResearch.MaxWorkers);
    }

    [Fact]
    public async Task Worker_runs_its_own_conversation_with_only_policy_tools()
    {
        var fake = new FakeClaude(
            FakeClaude.ToolUse("l1", "research", new { subtopic = "carry forward" }),
            FakeClaude.ToolUse("w1", "search_policies", new { query = "carry" }),
            FakeClaude.ToolUse("w2", "read_policy", new { id = "POL-04" }),
            FakeClaude.Text("Up to 5 days by 31 March [POL-04]"),
            FakeClaude.Text("You can carry forward up to 5 days [POL-04]."));
        var r = await Make(fake).AskAsync("Can I carry forward leave?");

        var workerFirst = fake.Requests[1];
        Assert.Single(workerFirst.Messages);  // only the subtopic, not the lead's history
        Assert.Equal(["search_policies", "read_policy"], workerFirst.Tools!.Select(t => t.TryPickTool(out var tool) ? tool.Name : "").ToList());
        var leadResult = Assert.Single(FakeClaude.ToolResults(fake.Requests[4]));
        Assert.Equal(("l1", "Up to 5 days by 31 March [POL-04]", false), leadResult);
        var worker = Assert.Single(r.Agents, a => a.Agent == "worker-1");
        Assert.Equal(3, worker.Turns);
        Assert.Equal(300, worker.InputTokens);
        Assert.Equal(60, worker.OutputTokens);
        Assert.Equal(600, r.TotalTokens);  // 5 replies x 120
    }

    [Fact]
    public async Task Worker_turn_limit_returns_Stopped_as_error()
    {
        var workerCalls = Enumerable.Range(1, PolicyResearch.MaxTurnsPerWorker)
            .Select(i => FakeClaude.ToolUse($"w{i}", "search_policies", new { query = "leave" }));
        var fake = new FakeClaude([
            FakeClaude.ToolUse("l1", "research", new { subtopic = "leave" }),
            .. workerCalls,
            FakeClaude.Text("The policies did not give an answer in time [POL-01].")]);
        var r = await Make(fake).AskAsync("Leave?");

        Assert.Equal(PolicyResearch.MaxTurnsPerWorker + 2, fake.Requests.Count);
        var (_, content, isError) = Assert.Single(FakeClaude.ToolResults(fake.Requests[^1]));
        Assert.StartsWith("Stopped:", content);
        Assert.True(isError);
        Assert.Equal(PolicyResearch.MaxTurnsPerWorker, r.Agents.Single(a => a.Agent == "worker-1").Turns);
    }

    [Fact]
    public async Task Worker_stops_at_token_budget()
    {
        // One worker reply that alone uses the whole budget.
        var huge = FakeClaude.ToolUse("w1", "search_policies", new { query = "leave" })
            .Replace("\"input_tokens\":100", $"\"input_tokens\":{PolicyResearch.TokenBudget}");
        var fake = new FakeClaude(
            FakeClaude.ToolUse("l1", "research", new { subtopic = "leave" }),
            huge,
            FakeClaude.Text("should never be sent"));
        var r = await Make(fake).AskAsync("Leave?");

        Assert.Equal(2, fake.Requests.Count);  // worker did not send again, lead did not continue
        Assert.Equal("token_budget", r.StoppedBy);
        var worker = r.Agents.Single(a => a.Agent == "worker-1");
        Assert.Equal(1, worker.Turns);
        Assert.Equal(PolicyResearch.TokenBudget, worker.InputTokens);
    }

    [Fact]
    public void Citation_after_the_full_stop_counts()
    {
        Assert.Empty(PolicyResearch.UncitedSentences("You can carry forward 5 days. [POL-04] Half days deduct 0.5. [POL-03]"));
        Assert.Equal(["Yes, you can do both."], PolicyResearch.UncitedSentences("Yes, you can do both. Half days deduct 0.5. [POL-03]"));
    }

    [Fact]
    public void Citation_before_the_full_stop_counts() =>
        Assert.Empty(PolicyResearch.UncitedSentences("You can carry forward 5 days [POL-04]. Half days deduct 0.5 [POL-03]."));
}
