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
        // Form with no ticket fields
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
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm(ticketCount: 2));

        WithAuth(JwtTokenHelper.AdminToken());
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var list = await listResponse.Content.ReadFromJsonAsync<List<SubmissionListItem>>();
        var submissionId = list!.Last().Id; // Last = most recently submitted in this test

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
    public async Task Listing_WithAdminRole_Returns200WithList()
    {
        WithAuth(JwtTokenHelper.AdminToken());

        var response = await _client.GetAsync("/api/submissions/listing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("[");
    }

    [Fact]
    public async Task Accept_WithAdminRole_Returns200()
    {
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm());

        WithAuth(JwtTokenHelper.AdminToken());
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var list = await listResponse.Content.ReadFromJsonAsync<List<SubmissionListItem>>();
        var submissionId = list!.Last().Id; // Last = most recently submitted in this test

        var subResponse = await _client.GetAsync($"/api/submissions/retrieve?fileId={submissionId}");
        var submission = await subResponse.Content.ReadFromJsonAsync<SubmissionDetail>();
        var fileId = submission!.Files.First().Id;

        var model = new { fileId = submissionId, acceptedFiles = new[] { fileId } };
        var response = await _client.PostAsJsonAsync("/api/submissions/accept", model);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reject_WithAdminRole_Returns200()
    {
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm());

        WithAuth(JwtTokenHelper.AdminToken());
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var list = await listResponse.Content.ReadFromJsonAsync<List<SubmissionListItem>>();
        var submissionId = list!.First().Id;

        var model = new { fileId = submissionId, acceptedFiles = Array.Empty<Guid>() };
        var response = await _client.PostAsJsonAsync("/api/submissions/reject", model);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        // Upload as User, then remove as Admin (endpoint now requires Admin role)
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm());

        WithAuth(JwtTokenHelper.AdminToken());
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var list = await listResponse.Content.ReadFromJsonAsync<List<SubmissionListItem>>();
        var submissionId = list!.Last().Id;
        var subResponse = await _client.GetAsync($"/api/submissions/retrieve?fileId={submissionId}");
        var submission = await subResponse.Content.ReadFromJsonAsync<SubmissionDetail>();
        var fileId = submission!.Files.First().Id;

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

    private record SubmissionListItem(int Id, List<SubmissionFileItem> Files);
    private record SubmissionFileItem(Guid Id);
    private record SubmissionDetail(int Id, List<SubmissionTicketItem> Tickets, List<SubmissionFileItem> Files);
    private record SubmissionTicketItem(string AppearanceId, string FileNumberText);
    private record PriorSubmission(int SubmissionId, List<PriorSubmissionFileItem> Files);
    private record PriorSubmissionFileItem(Guid Id, string OriginalFileName);
}
