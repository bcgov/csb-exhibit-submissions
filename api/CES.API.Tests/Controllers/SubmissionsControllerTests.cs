using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    private static MultipartFormDataContent BuildSubmitForm()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("APP001"), "appearanceID" },
            { new StringContent("2026-01-01T09:00:00"), "appearanceDateTime" },
            { new StringContent("2026-01-01"), "shortDate" },
            { new StringContent("001"), "appearanceSequenceNumber" },
            { new StringContent("ADP"), "appearanceReasonCode" },
            { new StringContent("Criminal"), "courtListType" },
            { new StringContent("FILE001"), "fileNumberText" },
            { new StringContent("LOC001"), "locationId" },
            { new StringContent("Test Court"), "locationNameText" },
            { new StringContent("ROOM1"), "roomCode" },
            { new StringContent("Courtroom 1"), "roomText" },
            { new StringContent("Smith, John"), "accusedName" },
            { new StringContent("1980-01-01"), "accusedDOB" },
            { new StringContent("OFF001"), "officerNumber" }
        };

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
    public async Task Retrieve_WithAdminRole_Returns200()
    {
        WithAuth(JwtTokenHelper.UserToken());
        await _client.PostAsync("/api/submissions/submit", BuildSubmitForm());

        WithAuth(JwtTokenHelper.AdminToken());
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var list = await listResponse.Content.ReadFromJsonAsync<List<SubmissionListItem>>();
        var submissionId = list!.First().Id;

        var response = await _client.GetAsync($"/api/submissions/retrieve?fileId={submissionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var submissionId = list!.First().Id;

        // Use retrieve (which includes files) to get file IDs
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

    private record SubmissionListItem(int Id, List<SubmissionFileItem> Files);
    private record SubmissionFileItem(Guid Id);
    private record SubmissionDetail(int Id, List<SubmissionFileItem> Files);
}
