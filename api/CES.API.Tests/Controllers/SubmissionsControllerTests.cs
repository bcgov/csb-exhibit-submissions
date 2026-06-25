using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CES.API.Tests.Fixtures;
using FluentAssertions;

namespace CES.API.Tests.Controllers;

public class SubmissionsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SubmissionsControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static MultipartFormDataContent BuildSubmitForm(int ticketCount = 1)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("2026-01-01"), "shortDate" },
            { new StringContent("LOC001"), "locationId" },
            { new StringContent("Test Court"), "locationNameText" },
            { new StringContent("ROOM1"), "roomCode" },
            { new StringContent("Courtroom 1"), "roomText" },
            { new StringContent("OFF001"), "officerNumber" },
        };

        for (var i = 0; i < ticketCount; i++)
        {
            form.Add(new StringContent($"APP{i:D3}"), $"tickets[{i}].appearanceId");
            form.Add(new StringContent("2026-01-01T09:00:00"), $"tickets[{i}].appearanceDateTime");
            form.Add(new StringContent($"{i + 1}"), $"tickets[{i}].appearanceSequenceNumber");
            form.Add(new StringContent("TRI"), $"tickets[{i}].appearanceReasonCode");
            form.Add(new StringContent("Criminal"), $"tickets[{i}].courtListType");
            form.Add(new StringContent($"FILE{i:D3}"), $"tickets[{i}].fileNumberText");
            form.Add(new StringContent("Smith, John"), $"tickets[{i}].accusedName");
            form.Add(new StringContent("1980-01-01"), $"tickets[{i}].accusedDOB");
        }

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake video content"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
        form.Add(fileContent, "files", "evidence.mp4");

        return form;
    }

    private HttpClient WithAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    // Helper: submit as user, return submission id via listing as admin
    private async Task<int> SubmitAndGetId(int ticketCount = 1)
    {
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm(ticketCount));

        WithAuth(JwtTokenHelper.AdminToken());
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var paged = await listResponse.Content.ReadFromJsonAsync<PagedResult>();
        // Listing is ordered newest-first, so the just-submitted record is first.
        return paged!.Items.First().Id;
    }

    // Helper: get first file id for a submission
    private async Task<Guid> GetFirstFileId(int submissionId)
    {
        WithAuth(JwtTokenHelper.AdminToken());
        var subResponse = await _client.GetAsync($"/api/submissions/retrieve?fileId={submissionId}");
        var submission = await subResponse.Content.ReadFromJsonAsync<SubmissionDetail>();
        return submission!.Files.First().Id;
    }

    [Fact]
    public async Task Submit_WithUserRole_Returns200()
    {
        WithAuth(JwtTokenHelper.UserToken());
        var form = BuildSubmitForm();

        var response = await _client.PostAsync("/api/submissions/submit", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_MultipleTickets_Returns200()
    {
        WithAuth(JwtTokenHelper.UserToken());
        var form = BuildSubmitForm(ticketCount: 3);

        var response = await _client.PostAsync("/api/submissions/submit", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_WithoutTickets_Returns400()
    {
        WithAuth(JwtTokenHelper.UserToken());
        var form = new MultipartFormDataContent
        {
            { new StringContent("2026-01-01"), "shortDate" },
            { new StringContent("LOC001"), "locationId" },
            { new StringContent("ROOM1"), "roomCode" },
            { new StringContent("OFF001"), "officerNumber" },
        };
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
        form.Add(fileContent, "files", "evidence.mp4");

        var response = await _client.PostAsync("/api/submissions/submit", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var form = BuildSubmitForm();

        var response = await _client.PostAsync("/api/submissions/submit", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Submit_WithAdminRole_Returns403()
    {
        WithAuth(JwtTokenHelper.AdminToken());
        var form = BuildSubmitForm();

        var response = await _client.PostAsync("/api/submissions/submit", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Retrieve_WithAdminRole_Returns200WithTickets()
    {
        var submissionId = await SubmitAndGetId(ticketCount: 2);

        WithAuth(JwtTokenHelper.AdminToken());
        var response = await _client.GetAsync($"/api/submissions/retrieve?fileId={submissionId}");
        var body = await response.Content.ReadFromJsonAsync<SubmissionDetail>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Tickets.Should().HaveCount(2);
    }

    [Fact]
    public async Task Retrieve_WithUserRole_Returns403()
    {
        WithAuth(JwtTokenHelper.UserToken());

        var response = await _client.GetAsync("/api/submissions/retrieve?fileId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Listing_WithAdminRole_Returns200WithPagedResult()
    {
        WithAuth(JwtTokenHelper.AdminToken());

        var response = await _client.GetAsync("/api/submissions/listing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task Listing_ShowsAllStatuses_IncludingAcceptedAndRejected()
    {
        // Submit two; accept one to move it to Accepted status (requires entering first)
        var id1 = await SubmitAndGetId();
        var id2 = await SubmitAndGetId();

        // Enter the file on id2 so it's acceptable, then accept
        var fileId2 = await GetFirstFileId(id2);
        WithAuth(JwtTokenHelper.AdminToken());
        await _client.PostAsJsonAsync($"/api/files/{fileId2}/enter", new { enteredValue = "1" });
        await _client.PostAsJsonAsync("/api/submissions/accept", new { submissionId = id2 });

        WithAuth(JwtTokenHelper.AdminToken());
        var response = await _client.GetAsync("/api/submissions/listing");
        var paged = await response.Content.ReadFromJsonAsync<PagedResult>();

        paged!.Items.Should().Contain(i => i.Status == "Accepted");
        paged.Items.Should().Contain(i => i.Status == "Pending");
    }

    [Fact]
    public async Task Accept_WithReadyExhibits_Returns200()
    {
        var submissionId = await SubmitAndGetId();
        var fileId = await GetFirstFileId(submissionId);

        // Enter the exhibit so the submission is ready to accept
        WithAuth(JwtTokenHelper.AdminToken());
        await _client.PostAsJsonAsync($"/api/files/{fileId}/enter", new { enteredValue = "1" });

        var response = await _client.PostAsJsonAsync("/api/submissions/accept", new { submissionId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Accept_WithUnclassifiedExhibit_Returns422()
    {
        var submissionId = await SubmitAndGetId();

        WithAuth(JwtTokenHelper.AdminToken());
        var response = await _client.PostAsJsonAsync("/api/submissions/accept", new { submissionId });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Accept_AlreadyAccepted_Returns422()
    {
        var submissionId = await SubmitAndGetId();
        var fileId = await GetFirstFileId(submissionId);

        WithAuth(JwtTokenHelper.AdminToken());
        await _client.PostAsJsonAsync($"/api/files/{fileId}/enter", new { enteredValue = "1" });
        await _client.PostAsJsonAsync("/api/submissions/accept", new { submissionId });

        // Try to accept again
        var response = await _client.PostAsJsonAsync("/api/submissions/accept", new { submissionId });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reject_WithAdminRole_Returns200()
    {
        var submissionId = await SubmitAndGetId();

        WithAuth(JwtTokenHelper.AdminToken());
        var response = await _client.PostAsJsonAsync("/api/submissions/reject", new { submissionId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reject_AlreadyRejected_Returns422()
    {
        var submissionId = await SubmitAndGetId();

        WithAuth(JwtTokenHelper.AdminToken());
        await _client.PostAsJsonAsync("/api/submissions/reject", new { submissionId });

        var response = await _client.PostAsJsonAsync("/api/submissions/reject", new { submissionId });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetByFileNumber_WithUserRole_Returns200WithResults()
    {
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm());

        var response = await _client.GetAsync("/api/submissions/by-file-number?fileNumberText=FILE000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("[");
    }

    [Fact]
    public async Task GetByFileNumber_UnknownFileNumber_Returns200EmptyArray()
    {
        WithAuth(JwtTokenHelper.UserToken());

        var response = await _client.GetAsync("/api/submissions/by-file-number?fileNumberText=UNKNOWN");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("[]");
    }

    [Fact]
    public async Task GetByFileNumber_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/submissions/by-file-number?fileNumberText=FILE000");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveFile_WithAdminRole_Returns200_WhenFileExists()
    {
        var submissionId = await SubmitAndGetId();
        var fileId = await GetFirstFileId(submissionId);

        WithAuth(JwtTokenHelper.AdminToken());
        var response = await _client.DeleteAsync($"/api/submissions/files/{fileId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveFile_CanRemoveEnteredExhibit_WhenPending()
    {
        var submissionId = await SubmitAndGetId();
        var fileId = await GetFirstFileId(submissionId);

        WithAuth(JwtTokenHelper.AdminToken());
        await _client.PostAsJsonAsync($"/api/files/{fileId}/enter", new { enteredValue = "3" });

        var response = await _client.DeleteAsync($"/api/submissions/files/{fileId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveFile_WithAdminRole_Returns404_WhenFileNotFound()
    {
        WithAuth(JwtTokenHelper.AdminToken());

        var response = await _client.DeleteAsync($"/api/submissions/files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveFile_WithUserRole_Returns403()
    {
        WithAuth(JwtTokenHelper.UserToken());

        var response = await _client.DeleteAsync($"/api/submissions/files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveFile_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.DeleteAsync($"/api/submissions/files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveFile_OnAcceptedSubmission_Returns409()
    {
        var submissionId = await SubmitAndGetId();
        var fileId = await GetFirstFileId(submissionId);

        WithAuth(JwtTokenHelper.AdminToken());
        await _client.PostAsJsonAsync($"/api/files/{fileId}/enter", new { enteredValue = "1" });
        await _client.PostAsJsonAsync("/api/submissions/accept", new { submissionId });

        var response = await _client.DeleteAsync($"/api/submissions/files/{fileId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private record PagedResult(List<SubmissionListItem> Items, int TotalCount, int Page, int PageSize);
    private record SubmissionListItem(int Id, string Status, int ExhibitCount);
    private record SubmissionDetail(int Id, List<SubmissionTicketItem> Tickets, List<SubmissionFileItem> Files);
    private record SubmissionTicketItem(string AppearanceId, string FileNumberText);
    private record SubmissionFileItem(Guid Id, string Status);
}
