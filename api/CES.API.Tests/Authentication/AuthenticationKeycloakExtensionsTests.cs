using System.Security.Claims;
using CES.API.Authentication;
using CES.Business.Constants;

namespace CES.API.Tests.Authentication;

/// <summary>
/// Role mapping and the azp trust boundary, exercised directly on a hand-built principal so
/// no realm or JWT handler is involved. Covers the two claim shapes seen in practice: a
/// separate claim per role, and a single claim holding the JSON-array string (the shape that
/// produced a real 403 in manual testing).
/// </summary>
public class AuthenticationKeycloakExtensionsTests
{
    private const string ClientId = "ces-test-client";

    // ---------- azp trust boundary ----------

    [Fact]
    public void IsAuthorizedParty_WhenAzpMatchesTheClient_IsTrue()
    {
        var principal = PrincipalWith(azp: ClientId);

        AuthenticationKeycloakExtensions.IsAuthorizedParty(principal, ClientId).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedParty_WhenAzpIsADifferentClient_IsFalse()
    {
        // A token minted for another realm client is validly signed but must not be accepted.
        var principal = PrincipalWith(azp: "some-other-client");

        AuthenticationKeycloakExtensions.IsAuthorizedParty(principal, ClientId).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParty_WhenAzpIsAbsent_IsFalse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        AuthenticationKeycloakExtensions.IsAuthorizedParty(principal, ClientId).Should().BeFalse();
    }

    // ---------- flat roles claim, one claim per role ----------

    [Fact]
    public void MapRoles_FromSeparateFlatRoleClaims_MapsEachKeycloakRole()
    {
        var principal = PrincipalWith(
            azp: ClientId,
            separateRoleClaims: [AuthConstants.KeycloakRoleAdmin, AuthConstants.KeycloakRoleUser, AuthConstants.KeycloakRoleClerk]);

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId)
            .Should().BeEquivalentTo([RoleConstants.Admin, RoleConstants.User, RoleConstants.Clerk]);
    }

    // ---------- flat roles claim, single JSON-array-string value ----------

    [Fact]
    public void MapRoles_FromASingleJsonArrayRoleClaim_MapsEachKeycloakRole()
    {
        // This is how the JwtBearer handler surfaced the array in the field, and why the
        // reader has to expand a bracketed value rather than treat it as one opaque role.
        var principal = PrincipalWith(
            azp: ClientId,
            jsonArrayRoleClaim: """["ces-judicial","ces-user","ces-clerk"]""");

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId)
            .Should().BeEquivalentTo([RoleConstants.Admin, RoleConstants.User, RoleConstants.Clerk]);
    }

    // ---------- resource_access fallback ----------

    [Fact]
    public void MapRoles_WhenNoFlatClaim_FallsBackToResourceAccess()
    {
        var resourceAccess = $$"""
            { "{{ClientId}}": { "roles": ["ces-judicial"] } }
            """;
        var principal = PrincipalWith(azp: ClientId, resourceAccess: resourceAccess);

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId)
            .Should().ContainSingle().Which.Should().Be(RoleConstants.Admin);
    }

    [Fact]
    public void MapRoles_IgnoresResourceAccessRolesForOtherClients()
    {
        var resourceAccess = """
            { "another-client": { "roles": ["ces-judicial"] } }
            """;
        var principal = PrincipalWith(azp: ClientId, resourceAccess: resourceAccess);

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId).Should().BeEmpty();
    }

    // ---------- unknown / empty ----------

    [Fact]
    public void MapRoles_DropsRolesWithNoCesMapping()
    {
        var principal = PrincipalWith(
            azp: ClientId,
            separateRoleClaims: ["ces-judicial", "some-unrelated-role", "account"]);

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId)
            .Should().ContainSingle().Which.Should().Be(RoleConstants.Admin);
    }

    [Fact]
    public void MapRoles_WhenThereAreNoRoleClaims_ReturnsEmpty()
    {
        var principal = PrincipalWith(azp: ClientId);

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId).Should().BeEmpty();
    }

    [Fact]
    public void MapRoles_DeduplicatesRepeatedRoles()
    {
        var principal = PrincipalWith(
            azp: ClientId,
            separateRoleClaims: ["ces-judicial", "ces-judicial"]);

        AuthenticationKeycloakExtensions.MapRoles(principal, ClientId)
            .Should().ContainSingle().Which.Should().Be(RoleConstants.Admin);
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal PrincipalWith(
        string? azp = null,
        IEnumerable<string>? separateRoleClaims = null,
        string? jsonArrayRoleClaim = null,
        string? resourceAccess = null)
    {
        var claims = new List<Claim>();

        if (azp is not null)
            claims.Add(new Claim(AuthConstants.AuthorizedPartyClaim, azp));

        if (separateRoleClaims is not null)
            claims.AddRange(separateRoleClaims.Select(role => new Claim(AuthConstants.RolesClaim, role)));

        if (jsonArrayRoleClaim is not null)
            claims.Add(new Claim(AuthConstants.RolesClaim, jsonArrayRoleClaim));

        if (resourceAccess is not null)
            claims.Add(new Claim(AuthConstants.ResourceAccessClaim, resourceAccess));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
