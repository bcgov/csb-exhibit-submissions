using CES.Business.Constants;
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
        _service = new UserService(_db);
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
    public async Task UpsertFromToken_StampsCreatedByAsTheUserItself()
    {
        await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        // A login provisions its own row, so the audit FK points back at that row rather
        // than being left null and unattributable.
        var row = await _db.ApplicationUser.SingleAsync();
        row.CreatedByUserId.Should().Be(row.Id);
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
        row.UpdatedByUserId.Should().Be(row.Id);
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

    // ── UpsertMockUserAsync (dev-bypass login) ────────────────────────────

    [Fact]
    public async Task UpsertMockUser_InsertsARowWithNoRealmSubject()
    {
        await _service.UpsertMockUserAsync("officer@gov.bc.ca", "Dev", "Officer");

        var row = await _db.ApplicationUser.SingleAsync();
        row.Email.Should().Be("officer@gov.bc.ca");
        row.KeycloakSub.Should().BeNull();
        row.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertMockUser_OnSecondLogin_UpdatesRatherThanDuplicating()
    {
        await _service.UpsertMockUserAsync("officer@gov.bc.ca", "Dev", "Officer");
        await _service.UpsertMockUserAsync("officer@gov.bc.ca", "Dev", "Officer");

        (await _db.ApplicationUser.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertMockUser_WithABlankEmail_Throws()
    {
        var act = () => _service.UpsertMockUserAsync("  ", "Dev", "Officer");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── ResolveUserIdAsync (what the audit columns are stamped with) ──────

    [Fact]
    public async Task ResolveUserId_MatchesOnRealmSubject()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        (await _service.ResolveUserIdAsync(Sub, "bryce.martel@gov.bc.ca")).Should().Be(user.Id);
    }

    [Fact]
    public async Task ResolveUserId_FallsBackToEmail_WhenTheSubjectIsUnknown()
    {
        // The mock dev-bypass token carries the email as its subject and has no realm sub.
        var user = await _service.UpsertMockUserAsync("officer@gov.bc.ca", "Dev", "Officer");

        (await _service.ResolveUserIdAsync("officer@gov.bc.ca", null)).Should().Be(user.Id);
    }

    [Fact]
    public async Task ResolveUserId_MatchesEmailCaseInsensitively()
    {
        var user = await _service.UpsertMockUserAsync("officer@gov.bc.ca", "Dev", "Officer");

        (await _service.ResolveUserIdAsync(null, "Officer@GOV.BC.CA")).Should().Be(user.Id);
    }

    [Fact]
    public async Task ResolveUserId_PrefersTheSubjectMatch_OverTheEmailMatch()
    {
        var bySub = await _service.UpsertFromTokenAsync(Sub, "shared@gov.bc.ca", "Bryce", "Martel");
        await _service.UpsertMockUserAsync("other@gov.bc.ca", "Dev", "Officer");

        (await _service.ResolveUserIdAsync(Sub, "other@gov.bc.ca")).Should().Be(bySub.Id);
    }

    [Fact]
    public async Task ResolveUserId_ReturnsNull_WhenNothingMatches()
    {
        // An unattributed change is recorded rather than the request being failed.
        (await _service.ResolveUserIdAsync("unknown-sub", "nobody@gov.bc.ca")).Should().BeNull();
    }

    [Fact]
    public async Task ResolveUserId_ReturnsNull_WhenNoIdentityIsSupplied()
    {
        (await _service.ResolveUserIdAsync(null, null)).Should().BeNull();
    }

    // ── GetProfileAsync (what the SPA prefills the submission form from) ──

    [Fact]
    public async Task GetProfile_ReturnsTheRow_MatchedOnTheRealmSubject()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");
        await _service.SetOfficerNumberAsync(user.Id, "PC-1234");

        var profile = await _service.GetProfileAsync(Sub, "bryce.martel@gov.bc.ca");

        profile.Should().NotBeNull();
        profile!.Id.Should().Be(user.Id);
        profile.Email.Should().Be("bryce.martel@gov.bc.ca");
        profile.OfficerNumber.Should().Be("PC-1234");
    }

    [Fact]
    public async Task GetProfile_FallsBackToEmail_ForTheMockToken()
    {
        var user = await _service.UpsertMockUserAsync("officer@gov.bc.ca", "Dev", "Officer");

        // The mock token carries the email as its subject, exactly as ResolveUserId handles it.
        (await _service.GetProfileAsync("officer@gov.bc.ca", null))!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetProfile_ReturnsNull_WhenNoLocalRowMatches()
    {
        (await _service.GetProfileAsync("unknown-sub", "nobody@gov.bc.ca")).Should().BeNull();
    }

    [Fact]
    public async Task GetProfile_ReturnsANullOfficerNumber_BeforeOneIsSupplied()
    {
        await _service.UpsertFromTokenAsync(Sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        (await _service.GetProfileAsync(Sub, null))!.OfficerNumber.Should().BeNull();
    }

    // ── SetOfficerNumberAsync ────────────────────────────────────────────

    [Fact]
    public async Task SetOfficerNumber_PersistsTheTrimmedValue()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");

        var profile = await _service.SetOfficerNumberAsync(user.Id, "  PC1234  ");

        profile.OfficerNumber.Should().Be("PC1234");
        (await _db.ApplicationUser.SingleAsync()).OfficerNumber.Should().Be("PC1234");
    }

    [Fact]
    public async Task SetOfficerNumber_StampsTheUpdateAgainstTheUserItself()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");

        await _service.SetOfficerNumberAsync(user.Id, "PC1234");

        (await _db.ApplicationUser.SingleAsync()).UpdatedByUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task SetOfficerNumber_AllowsDashesAndPeriods()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");

        (await _service.SetOfficerNumberAsync(user.Id, "A-1.2")).OfficerNumber.Should().Be("A-1.2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetOfficerNumber_WithNoValue_Throws(string? officerNumber)
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");

        var act = () => _service.SetOfficerNumberAsync(user.Id, officerNumber);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetOfficerNumber_OverTheMaximumLength_Throws()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");
        var tooLong = new string('A', UserConstants.OfficerNumberMaxLength + 1);

        var act = () => _service.SetOfficerNumberAsync(user.Id, tooLong);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetOfficerNumber_AtTheMaximumLength_IsAccepted()
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");
        var atLimit = new string('A', UserConstants.OfficerNumberMaxLength);

        (await _service.SetOfficerNumberAsync(user.Id, atLimit)).OfficerNumber.Should().Be(atLimit);
    }

    [Theory]
    [InlineData("ABC 123")]   // spaces
    [InlineData("AB/12")]     // slash
    [InlineData("AB_12")]     // underscore
    [InlineData("<script>")]
    public async Task SetOfficerNumber_WithDisallowedCharacters_Throws(string officerNumber)
    {
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");

        var act = () => _service.SetOfficerNumberAsync(user.Id, officerNumber);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetOfficerNumber_ForAnUnknownUser_Throws()
    {
        var act = () => _service.SetOfficerNumberAsync(9999, "PC1234");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpsertFromToken_OnASubsequentLogin_LeavesTheOfficerNumberIntact()
    {
        // Identity columns are refreshed from IDIR on every login; the officer number is the
        // one CES-owned field on the row and must survive that refresh.
        var user = await _service.UpsertFromTokenAsync(Sub, "a@gov.bc.ca", "A", "One");
        await _service.SetOfficerNumberAsync(user.Id, "PC1234");

        await _service.UpsertFromTokenAsync(Sub, "renamed@gov.bc.ca", "A", "Newname");

        (await _db.ApplicationUser.SingleAsync()).OfficerNumber.Should().Be("PC1234");
    }
}
