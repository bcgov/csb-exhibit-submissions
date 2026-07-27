using CES.API.Authentication;
using FluentAssertions;

namespace CES.API.Tests.Authentication;

/// <summary>
/// The open-redirect guard. Anything that could send an officer off-site after a
/// successful IDIR login must collapse to the app root.
/// </summary>
public class ReturnUrlValidatorTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/officer/court-list")]
    [InlineData("/admin/view/42")]
    [InlineData("/admin/exhibit-search?fileNumber=12345")]
    public void Sanitize_WithSameSiteRelativePath_ReturnsItUnchanged(string returnUrl)
    {
        ReturnUrlValidator.Sanitize(returnUrl).Should().Be(returnUrl);
    }

    [Theory]
    [InlineData("//evil.example")]                 // protocol-relative
    [InlineData("/\\evil.example")]                // backslash variant some browsers normalize
    [InlineData("https://evil.example")]           // absolute
    [InlineData("http://evil.example/officer")]
    [InlineData("javascript:alert(1)")]            // scheme, not a path
    [InlineData("//evil.example/officer/court-list")]
    [InlineData("officer/court-list")]             // not rooted
    [InlineData("\\\\evil.example")]
    public void Sanitize_WithOffSiteTarget_FallsBackToRoot(string returnUrl)
    {
        ReturnUrlValidator.Sanitize(returnUrl).Should().Be(AuthConstants.DefaultReturnUrl);
    }

    [Theory]
    [InlineData("/%2Fevil.example")]   // decodes to "//evil.example"
    [InlineData("/%5Cevil.example")]   // decodes to "/\evil.example"
    public void Sanitize_WithEncodedOffSiteTarget_FallsBackToRoot(string returnUrl)
    {
        // The raw form passes the prefix checks; only validating the decoded form too
        // stops these.
        ReturnUrlValidator.Sanitize(returnUrl).Should().Be(AuthConstants.DefaultReturnUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_WithNoValue_FallsBackToRoot(string? returnUrl)
    {
        ReturnUrlValidator.Sanitize(returnUrl).Should().Be(AuthConstants.DefaultReturnUrl);
    }

    [Theory]
    [InlineData("/officer\r\nX-Injected: 1")]
    [InlineData("/officer\ncourt-list")]
    public void Sanitize_WithControlCharacters_FallsBackToRoot(string returnUrl)
    {
        ReturnUrlValidator.Sanitize(returnUrl).Should().Be(AuthConstants.DefaultReturnUrl);
    }
}
