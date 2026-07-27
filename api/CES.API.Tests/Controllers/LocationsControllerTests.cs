using System.Collections.ObjectModel;
using System.Net;
using CES.API.Tests.Fixtures;
using CES.Business.Models.Location;
using CES.Business.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CES.API.Tests.Controllers;

public class LocationsControllerTests
{
    private readonly HttpClient _client;

    public LocationsControllerTests()
    {
        var mockLocationService = new Mock<ILocationService>();
        mockLocationService
            .Setup(s => s.GetJCLocations(It.IsAny<bool>()))
            .ReturnsAsync(new Collection<Location> { new() { LocationId = "LOC001", Name = "Test Court" } });

        var mockCourtListService = new Mock<ICourtListService>();
        mockCourtListService
            .Setup(s => s.GetJCCourtList(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Collection<CourtList> { new() { AppearanceId = "APP001" } });

        var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var locationDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ILocationService));
                if (locationDescriptor != null)
                    services.Remove(locationDescriptor);

                var courtListDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ICourtListService));
                if (courtListDescriptor != null)
                    services.Remove(courtListDescriptor);

                services.AddScoped(_ => mockLocationService.Object);
                services.AddScoped(_ => mockCourtListService.Object);
            });
        });

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLocations_Returns200WithData()
    {
        var response = await _client.GetAsync("/api/location/getLocations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("LOC001");
    }

    [Fact]
    public async Task GetCourtList_Returns200WithData()
    {
        var response = await _client.GetAsync(
            "/api/files/getCourtList?agencyId=LOC001&roomCode=ROOM1&proceedingDate=2026-01-15");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("APP001");
    }
}
