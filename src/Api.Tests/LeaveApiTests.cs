using System.Net;
using System.Net.Http.Json;

namespace Api.Tests;

// All tests share one API database, so each uses its own dates in Feb 2027 (1 Feb 2027 is a Monday).
// Farah (6) has 15 days available; these tests use at most 9 of them.
public class LeaveApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Employee_creates_leave_for_themselves_with_days()
    {
        var http = await api.SignedInAs(People.Farah);
        var res = await http.PostAsJsonAsync("/leave", new { employeeId = 7, startDate = "2027-02-05", endDate = "2027-02-05", halfDay = "None", reason = "Errand" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = (await res.Content.ReadFromJsonAsync<LeaveRow>())!;
        Assert.Equal(6, created.EmployeeId); // the body's employeeId is ignored
        Assert.Equal(1, created.Days);
        Assert.Equal("Approved", created.Status);

        var rows = await http.GetFromJsonAsync<List<LeaveRow>>("/leave?employeeId=6");
        Assert.Equal(1, rows!.Single(r => r.Id == created.Id).Days);
    }

    [Theory]
    [InlineData("{\"endDate\":\"2027-02-08\"}")]
    [InlineData("{\"startDate\":\"2027-02-08\"}")]
    public async Task Missing_date_is_400(string body)
    {
        var http = await api.SignedInAs(People.Farah);
        var res = await http.PostAsync("/leave", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Weekend_only_is_400_with_a_message()
    {
        var http = await api.SignedInAs(People.Farah);
        var res = await http.PostAsJsonAsync("/leave", new { startDate = "2027-02-06", endDate = "2027-02-07", halfDay = "None" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<Problem>();
        Assert.False(string.IsNullOrWhiteSpace(problem!.Title));
    }

    [Fact]
    public async Task Only_the_manager_approves_and_only_once()
    {
        var farah = await api.SignedInAs(People.Farah);
        var id = await CreatePending(farah, "2027-02-15", "2027-02-18");

        var gopal = await api.SignedInAs(People.Gopal);
        Assert.Equal(HttpStatusCode.Forbidden, (await gopal.PostAsync($"/leave/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farah.PostAsync($"/leave/{id}/approve", null)).StatusCode);

        var aina = await api.SignedInAs(People.Aina);
        var ok = await aina.PostAsync($"/leave/{id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Approved", (await ok.Content.ReadFromJsonAsync<LeaveRow>())!.Status);

        Assert.Equal(HttpStatusCode.BadRequest, (await aina.PostAsync($"/leave/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await aina.PostAsync($"/leave/{id}/reject", null)).StatusCode);
    }

    [Fact]
    public async Task Another_employee_gets_403_on_reject_and_HR_may_reject()
    {
        var farah = await api.SignedInAs(People.Farah);
        var id = await CreatePending(farah, "2027-02-22", "2027-02-25");

        var gopal = await api.SignedInAs(People.Gopal);
        Assert.Equal(HttpStatusCode.Forbidden, (await gopal.PostAsync($"/leave/{id}/reject", null)).StatusCode);

        var dina = await api.SignedInAs(People.Dina);
        var res = await dina.PostAsync($"/leave/{id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Rejected", (await res.Content.ReadFromJsonAsync<LeaveRow>())!.Status);
    }

    [Fact]
    public async Task Unknown_request_is_404()
    {
        var aina = await api.SignedInAs(People.Aina);
        Assert.Equal(HttpStatusCode.NotFound, (await aina.PostAsync("/leave/99999/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aina.PostAsync("/leave/99999/reject", null)).StatusCode);
    }

    static async Task<int> CreatePending(HttpClient http, string start, string end)
    {
        var res = await http.PostAsJsonAsync("/leave", new { startDate = start, endDate = end, halfDay = "None", reason = "Trip" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var row = (await res.Content.ReadFromJsonAsync<LeaveRow>())!;
        Assert.Equal("Pending", row.Status);
        return row.Id;
    }

    record LeaveRow(int Id, int EmployeeId, string Status, decimal Days);
    record Problem(string Title);
}
