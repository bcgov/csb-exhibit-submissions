using System.Net;
using System.Security.Cryptography;
using System.Text;
using CES.API;
using CES.API.Authentication;
using CES.API.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;

namespace CES.API.Tests.Authentication;

public class KeycloakTokenServiceTests
{
    // Placeholder realm values — no real Authority, client id, or secret belongs in a
    // tracked file. The secret is deliberately distinctive so the "never logged"
    // assertion below cannot pass by coincidence.
    private const string Authority = "https://keycloak.test/realms/ces";
    private const string ClientId = "ces-test-client";
    private const string ClientSecret = "s3cr3t-must-never-be-logged-9f2a";
    private const string RedirectUri = "http://localhost:9080/auth/callback";
    private const string PostLogoutRedirectUri = "http://localhost:9080/";

    private const string AuthorizeEndpoint = Authority + "/protocol/openid-connect/auth";
    private const string TokenEndpoint = Authority + "/protocol/openid-connect/token";
    private const string EndSessionEndpoint = Authority + "/protocol/openid-connect/logout";

    private const string SuccessBody = """
        {
          "access_token": "access-token-value",
          "refresh_token": "refresh-token-value",
          "id_token": "id-token-value",
          "expires_in": 300,
          "refresh_expires_in": 1800,
          "token_type": "Bearer"
        }
        """;

    private const string InvalidGrantBody = """
        { "error": "invalid_grant", "error_description": "Code not valid" }
        """;

    private static KeycloakConfiguration Configuration() => new()
    {
        Enabled = true,
        Authority = Authority,
        Client = ClientId,
        Secret = ClientSecret,
        RedirectUri = RedirectUri,
        PostLogoutRedirectUri = PostLogoutRedirectUri,
    };

    private static Mock<IConfigurationManager<OpenIdConnectConfiguration>> Discovery()
    {
        var configuration = new OpenIdConnectConfiguration
        {
            AuthorizationEndpoint = AuthorizeEndpoint,
            TokenEndpoint = TokenEndpoint,
            EndSessionEndpoint = EndSessionEndpoint,
        };

        var manager = new Mock<IConfigurationManager<OpenIdConnectConfiguration>>();
        manager
            .Setup(m => m.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        return manager;
    }

    private static (KeycloakTokenService Service, StubHttpMessageHandler Handler, CapturingLogger<KeycloakTokenService> Logger)
        CreateService(HttpStatusCode statusCode = HttpStatusCode.OK, string body = SuccessBody)
    {
        var handler = new StubHttpMessageHandler(statusCode, body);
        var logger = new CapturingLogger<KeycloakTokenService>();
        var service = new KeycloakTokenService(
            new HttpClient(handler), Configuration(), Discovery().Object, logger);

        return (service, handler, logger);
    }

    // ---------- PKCE + authorize request ----------

    [Fact]
    public async Task BuildAuthorizeRequest_ProducesAnRfc7636CodeVerifier()
    {
        var (service, _, _) = CreateService();

        var (_, state) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);

        state.CodeVerifier.Length.Should().BeInRange(43, 128);
        state.CodeVerifier.Should().MatchRegex("^[A-Za-z0-9-_]+$", "base64url has no +, / or = padding");
    }

    [Fact]
    public async Task BuildAuthorizeRequest_ChallengeIsBase64UrlSha256OfVerifier()
    {
        var (service, _, _) = CreateService();

        var (authorizeUrl, state) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);

        var expected = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(state.CodeVerifier)));

        QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query)[AuthConstants.OAuth.CodeChallenge]
            .ToString().Should().Be(expected);
    }

    [Fact]
    public async Task BuildAuthorizeRequest_SendsS256AndIdirHintAndConfiguredRedirectUri()
    {
        var (service, _, _) = CreateService();

        var (authorizeUrl, state) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);
        var query = QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query);

        authorizeUrl.Should().StartWith(AuthorizeEndpoint);
        query[AuthConstants.OAuth.CodeChallengeMethod].ToString()
            .Should().Be(AuthConstants.OAuth.CodeChallengeMethodS256);
        query[AuthConstants.OAuth.IdpHintParameter].ToString().Should().Be(AuthConstants.IdpHint);
        query[AuthConstants.OAuth.ClientId].ToString().Should().Be(ClientId);
        query[AuthConstants.OAuth.ResponseType].ToString().Should().Be(AuthConstants.OAuth.ResponseTypeCode);
        query[AuthConstants.OAuth.RedirectUri].ToString().Should().Be(RedirectUri);
        query[AuthConstants.OAuth.State].ToString().Should().Be(state.State);
    }

    [Fact]
    public async Task BuildAuthorizeRequest_NeverPutsTheSecretInTheAuthorizeUrl()
    {
        var (service, _, _) = CreateService();

        var (authorizeUrl, _) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);

        authorizeUrl.Should().NotContain(ClientSecret);
    }

    [Fact]
    public async Task BuildAuthorizeRequest_GeneratesFreshStateAndVerifierEachCall()
    {
        var (service, _, _) = CreateService();

        var (_, first) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);
        var (_, second) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);

        second.State.Should().NotBe(first.State);
        second.CodeVerifier.Should().NotBe(first.CodeVerifier);
    }

    [Fact]
    public async Task BuildAuthorizeRequest_SanitizesTheReturnUrlBeforeStoringIt()
    {
        var (service, _, _) = CreateService();

        var (_, safe) = await service.BuildAuthorizeRequestAsync("/officer/court-list", CancellationToken.None);
        var (_, hostile) = await service.BuildAuthorizeRequestAsync("//evil.example", CancellationToken.None);

        safe.ReturnUrl.Should().Be("/officer/court-list");
        hostile.ReturnUrl.Should().Be(AuthConstants.DefaultReturnUrl);
    }

    // ---------- Code exchange ----------

    [Fact]
    public async Task ExchangeCode_OnSuccess_ParsesAllTokens()
    {
        var (service, _, _) = CreateService();

        var tokens = await service.ExchangeCodeAsync("the-code", "the-verifier", CancellationToken.None);

        tokens.AccessToken.Should().Be("access-token-value");
        tokens.RefreshToken.Should().Be("refresh-token-value");
        tokens.IdToken.Should().Be("id-token-value");
        tokens.ExpiresIn.Should().Be(300);
    }

    [Fact]
    public async Task ExchangeCode_AuthenticatesWithClientSecretAsAFormField()
    {
        var (service, handler, _) = CreateService();

        await service.ExchangeCodeAsync("the-code", "the-verifier", CancellationToken.None);

        handler.RequestUri!.ToString().Should().Be(TokenEndpoint);
        handler.Form[AuthConstants.OAuth.GrantType]
            .Should().Be(AuthConstants.OAuth.GrantTypeAuthorizationCode);
        handler.Form[AuthConstants.OAuth.Code].Should().Be("the-code");
        handler.Form[AuthConstants.OAuth.CodeVerifier].Should().Be("the-verifier");
        handler.Form[AuthConstants.OAuth.ClientId].Should().Be(ClientId);
        handler.Form[AuthConstants.OAuth.ClientSecret].Should().Be(ClientSecret);
    }

    [Fact]
    public async Task ExchangeCode_SendsTheConfiguredRedirectUriByteIdentically()
    {
        // Keycloak answers invalid_grant if this differs from the authorize request by
        // even a trailing slash, so it must come from configuration on both legs.
        var (service, handler, _) = CreateService();

        var (authorizeUrl, _) = await service.BuildAuthorizeRequestAsync(null, CancellationToken.None);
        await service.ExchangeCodeAsync("the-code", "the-verifier", CancellationToken.None);

        var onAuthorize = QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query)[AuthConstants.OAuth.RedirectUri].ToString();

        handler.Form[AuthConstants.OAuth.RedirectUri].Should().Be(RedirectUri);
        handler.Form[AuthConstants.OAuth.RedirectUri].Should().Be(onAuthorize);
    }

    [Fact]
    public async Task ExchangeCode_WhenKeycloakRejectsTheGrant_ThrowsArgumentException()
    {
        // ArgumentException is what ApiExceptionMiddleware maps to a 400.
        var (service, _, _) = CreateService(HttpStatusCode.BadRequest, InvalidGrantBody);

        var act = () => service.ExchangeCodeAsync("bad-code", "the-verifier", CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should().Contain("invalid_grant");
    }

    [Fact]
    public async Task ExchangeCode_OnFailure_NeverLeaksTheSecretIntoLogsOrTheException()
    {
        var (service, _, logger) = CreateService(HttpStatusCode.BadRequest, InvalidGrantBody);

        var act = () => service.ExchangeCodeAsync("bad-code", "the-verifier", CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        logger.Messages.Should().NotBeEmpty("the failure must still be diagnosable");
        logger.AllText.Should().NotContain(ClientSecret);
        logger.AllText.Should().Contain("invalid_grant");
        logger.AllText.Should().Contain("Code not valid");
        exception.Message.Should().NotContain(ClientSecret);
    }

    [Fact]
    public async Task ExchangeCode_OnSuccess_LogsNothingContainingTheSecret()
    {
        var (service, _, logger) = CreateService();

        await service.ExchangeCodeAsync("the-code", "the-verifier", CancellationToken.None);

        logger.AllText.Should().NotContain(ClientSecret);
    }

    [Fact]
    public async Task ExchangeCode_WhenKeycloakReturnsUnreadableJson_ThrowsArgumentException()
    {
        var (service, _, _) = CreateService(HttpStatusCode.OK, "not json at all");

        var act = () => service.ExchangeCodeAsync("the-code", "the-verifier", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------- Refresh ----------

    [Fact]
    public async Task Refresh_SendsTheRefreshGrantWithClientCredentials()
    {
        var (service, handler, _) = CreateService();

        var tokens = await service.RefreshAsync("old-refresh-token", CancellationToken.None);

        handler.Form[AuthConstants.OAuth.GrantType].Should().Be(AuthConstants.OAuth.GrantTypeRefreshToken);
        handler.Form[AuthConstants.OAuth.RefreshToken].Should().Be("old-refresh-token");
        handler.Form[AuthConstants.OAuth.ClientId].Should().Be(ClientId);
        handler.Form[AuthConstants.OAuth.ClientSecret].Should().Be(ClientSecret);

        // Rotation-safe: whatever Keycloak returns is what gets written back.
        tokens.RefreshToken.Should().Be("refresh-token-value");
    }

    [Fact]
    public async Task Refresh_WhenTheGrantIsRejected_ThrowsArgumentException()
    {
        var (service, _, logger) = CreateService(HttpStatusCode.BadRequest, InvalidGrantBody);

        var act = () => service.RefreshAsync("expired-or-rotated", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        logger.AllText.Should().NotContain(ClientSecret);
    }

    // ---------- Logout ----------

    [Fact]
    public async Task BuildEndSessionUrl_IncludesTheIdTokenHintWhenAvailable()
    {
        var (service, _, _) = CreateService();

        var url = await service.BuildEndSessionUrlAsync("the-id-token", CancellationToken.None);
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);

        url.Should().StartWith(EndSessionEndpoint);
        query[AuthConstants.OAuth.IdTokenHint].ToString().Should().Be("the-id-token");
        query[AuthConstants.OAuth.PostLogoutRedirectUri].ToString().Should().Be(PostLogoutRedirectUri);
    }

    [Fact]
    public async Task BuildEndSessionUrl_OmitsTheIdTokenHintWhenThereIsNone()
    {
        var (service, _, _) = CreateService();

        var url = await service.BuildEndSessionUrlAsync(null, CancellationToken.None);

        QueryHelpers.ParseQuery(new Uri(url).Query)
            .Should().NotContainKey(AuthConstants.OAuth.IdTokenHint);
    }

    [Fact]
    public async Task BuildEndSessionUrl_NeverIncludesTheSecret()
    {
        var (service, _, _) = CreateService();

        var url = await service.BuildEndSessionUrlAsync("the-id-token", CancellationToken.None);

        url.Should().NotContain(ClientSecret);
    }
}
