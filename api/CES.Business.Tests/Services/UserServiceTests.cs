using CES.Business.Constants;
using CES.Business.Interfaces;
using CES.Business.Services;
using CES.EF;
using CES.Entities;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Tests.Services;

/// <summary>
/// The Keycloak-login upsert (Decision 19): one CES-local row per realm subject, refreshed
/// from the token each login, never carrying a credential or a persisted role.
/// </summary>
public class UserServiceTests : IDisposable
{
    private const string Sub = "827df02f-d284-49a9-84b0-b4893c107cb5";

    private readonly CESDataStore _db;
    private readonly UserService _service;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<CESDataStore>()
            .UseInMemoryDatabase($"UserServiceTests_{Guid.NewGuid()}")
            .Options;
        _db = new CESDataStore(options);

        // The password service is unused on this path but required by the constructor.
        _service = new UserService(_db, new Mock<IPasswordService>().Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task UpsertFromToken_OnFirstLogin_InsertsARowKeyedOnTheSubject()
    {
        await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        var row = await _db.ApplicationUser.SingleAsync();
        row.KeycloakSub.Should().Be(Sub);
        row.Email.Should().Be("bryce.martel@gov.bc.ca");
        row.FirstName.Should().Be("Bryce");
        row.LastName.Should().Be("Martel");
        row.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertFromToken_LeavesThePasswordUnset()
    {
        await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        // Keycloak owns authentication; a provisioned row must never hold a credential.
        var row = await _db.ApplicationUser.SingleAsync();
        row.Password.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertFromToken_StampsCreatedByKeycloak()
    {
        await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        var row = await _db.ApplicationUser.SingleAsync();
        row.CreatedBy.Should().Be(UserConstants.KeycloakProvisionedBy);
    }

    [Fact]
    public async Task UpsertFromToken_OnSecondLogin_UpdatesRatherThanDuplicating()
    {
        await _service.UpsertFromTokenAsync(Sub, "old@gov.bc.ca", "Bryce", "Martel");
        await _service.UpsertFromTokenAsync(Sub, "old@gov.bc.ca", "Bryce", "Martel");

        (await _db.ApplicationUser.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertFromToken_RefreshesNameAndEmailFromTheToken()
    {
        await _service.UpsertFromTokenAsync(Sub, "old@gov.bc.ca", "Bryce", "Martel");

        // The IDIR record changed between logins; the row must follow it, not drift.
        await _service.UpsertFromTokenAsync(Sub, "new@gov.bc.ca", "Bryce", "Newname");

        var row = await _db.ApplicationUser.SingleAsync();
        row.Email.Should().Be("new@gov.bc.ca");
        row.LastName.Should().Be("Newname");
        row.UpdatedBy.Should().Be(UserConstants.KeycloakProvisionedBy);
    }

    [Fact]
    public async Task UpsertFromToken_TreatsDistinctSubjectsAsDistinctUsers()
    {
        await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");
        await _service.UpsertFromTokenAsync("a-different-sub", "b@gov.bc.ca", "B", "Two");

        (await _db.ApplicationUser.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task UpsertFromToken_WithABlankSubject_Throws()
    {
        var act = () => _service.UpsertFromTokenAsync("  ", "a@gov.bc.ca", "A", "One");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertFromToken_WithNullClaims_StoresEmptyStringsNotNulls()
    {
        // The non-null columns must not be violated when a claim is absent from the token.
        await _service.UpsertFromTokenAsync(Sub, null, null, null);

        var row = await _db.ApplicationUser.SingleAsync();
        row.Email.Should().BeEmpty();
        row.FirstName.Should().BeEmpty();
        row.LastName.Should().BeEmpty();
    }
}
