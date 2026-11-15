using System.Net;
using System.Net.Http.Json;

namespace Api.Tests;

/// <summary>GET /holidays: national holidays, a state filter, and 400 for bad input.</summary>
public class HolidayApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    record Row(DateOnly Date, string Name, string State);

    async Task<Row[]> Get(string query)
    {
        var http = await api.SignedInAs(People.Farah);
        var res = await http.GetAsync($"/holidays?{query}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<Row[]>())!;
    }

    [Fact]
    public async Task All_returns_national_holidays_only()
    {
        var rows = await Get("state=ALL&year=2026");
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal("ALL", r.State));
        Assert.All(rows, r => Assert.Equal(2026, r.Date.Year));
        Assert.Contains(rows, r => r.Name == "Merdeka Day");
    }

    [Fact]
    public async Task State_filter_adds_that_states_holidays_and_no_others()
    {
        var rows = await Get("state=SEL&year=2026");
        Assert.Contains(rows, r => r.State == "SEL");
        Assert.Contains(rows, r => r.State == "ALL");
        Assert.DoesNotContain(rows, r => r.State is "KUL" or "PNG");
        Assert.Equal(rows.OrderBy(r => r.Date), rows); // sorted by date
    }

    [Fact]
    public async Task State_is_case_insensitive()
    {
        Assert.Contains(await Get("state=kul&year=2026"), r => r.State == "KUL");
    }

    [Fact]
    public async Task Year_with_no_data_returns_empty_list()
    {
        Assert.Empty(await Get("state=PNG&year=1990"));
    }

    [Theory]
    [InlineData("state=XYZ&year=2026")]
    [InlineData("year=2026")]
    [InlineData("state=SEL")]
    public async Task Unknown_state_or_missing_input_is_400_problem_details(string query)
    {
        var http = await api.SignedInAs(People.Farah);
        var res = await http.GetAsync($"/holidays?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/holidays?state=ALL&year=2026")).StatusCode);
    }
}
