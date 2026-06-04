using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CES.API.Tests.Fixtures;
using FluentAssertions;

namespace CES.API.Tests.Controllers;

public class FilesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FilesControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> UploadFileAndGetId()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.UserToken());

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

        await _client.PostAsync("/api/submissions/submit", form);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.AdminToken());

        // Get listing to find submission ID
        var listResponse = await _client.GetAsync("/api/submissions/listing");
        var list = await listResponse.Content.ReadFromJsonAsync<List<SubmissionListItem>>();
        var submissionId = list!.First().Id;

        // Use retrieve (which includes files) to get file ID
        var subResponse = await _client.GetAsync($"/api/submissions/retrieve?fileId={submissionId}");
        var submission = await subResponse.Content.ReadFromJsonAsync<SubmissionDetail>();
        return submission!.Files.First().Id;
    }

    [Fact]
    public async Task ViewFile_WithValidId_ReturnsFileStream()
    {
        var fileId = await UploadFileAndGetId();

        var response = await _client.GetAsync($"/api/files/{fileId}/view");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ViewFile_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/files/{Guid.NewGuid()}/view");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadFile_WithValidId_Returns200WithDispositionHeader()
    {
        var fileId = await UploadFileAndGetId();

        var response = await _client.GetAsync($"/api/files/{fileId}/download");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.DispositionType
            .Should().Be("attachment");
    }

    private record SubmissionListItem(int Id, List<SubmissionFileItem> Files);
    private record SubmissionFileItem(Guid Id);
    private record SubmissionDetail(int Id, List<SubmissionFileItem> Files);
}
