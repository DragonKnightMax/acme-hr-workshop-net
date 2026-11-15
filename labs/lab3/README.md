# Lab 3 — Multi-Agent Research System: policy research (35 min, runs 3rd)

**Goal:** a lead agent splits a policy question; workers research it with their own context; the answer cites a policy for every claim.

**Branch:** `git checkout lab3-start` · Solution: `lab4-start`. Prompts: [PROMPTS.md](../../PROMPTS.md#lab-3--policy-research-agents). 

## Steps
3.1 `git checkout lab3-start` and run `dotnet test`: 2 research tests fail. Read `src/Api/Research/PolicyResearch.cs`.

3.2 Paste **Finish the worker**: its own conversation, only `search_policies` and `read_policy`, `MaxTurnsPerWorker`. A worker that stops early returns text starting with `Stopped:`, so the lead sees an error, not a normal result.

3.3 Check the limits in code: `MaxWorkers`, `MaxTurnsPerWorker`, `TokenBudget`.

3.4 Restart the API, then paste **Ask a research question**. Then, in the web app as Farah, ask the same question on **Policies**.

## Done when
- [x] Every claim cites a policy id (`citationsValid` true)
- [x] The run stops inside your limits (`stoppedBy` end_turn)
- [x] Token use per agent is shown

## Debrief
When would one agent with more tools have been enough?
