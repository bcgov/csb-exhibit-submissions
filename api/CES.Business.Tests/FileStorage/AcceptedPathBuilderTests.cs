using CES.Business.FileStorage;
using FluentAssertions;

namespace CES.Business.Tests.FileStorage;

public class AcceptedPathBuilderTests
{
    // ── SanitizeSegment ───────────────────────────────────────────────────

    [Theory]
    [InlineData("LOC001", "loc001")]
    [InlineData("Room-1", "room-1")]
    [InlineData("20260101", "20260101")]
    public void SanitizeSegment_LowercasesAndKeepsAlnumAndDash(string input, string expected)
    {
        AcceptedPathBuilder.SanitizeSegment(input).Should().Be(expected);
    }

    [Fact]
    public void SanitizeSegment_IsIdempotent()
    {
        var once = AcceptedPathBuilder.SanitizeSegment("Room #1/../x");
        var twice = AcceptedPathBuilder.SanitizeSegment(once);
        twice.Should().Be(once);
    }

    [Theory]
    [InlineData("../../etc", "etc")]
    [InlineData("a/b/c", "abc")]
    [InlineData("a\\b", "ab")]
    [InlineData("room .1", "room1")]
    [InlineData("A..B", "ab")]
    public void SanitizeSegment_StripsSeparatorsDotsAndPunctuation(string input, string expected)
    {
        AcceptedPathBuilder.SanitizeSegment(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("....")]
    [InlineData("/../")]
    [InlineData("!!!")]
    public void SanitizeSegment_Throws_WhenNoAllowedCharacters(string input)
    {
        var act = () => AcceptedPathBuilder.SanitizeSegment(input);
        act.Should().Throw<PathTraversalException>();
    }

    [Fact]
    public void SanitizeSegment_Throws_WhenOverlong()
    {
        var act = () => AcceptedPathBuilder.SanitizeSegment(new string('a', 200));
        act.Should().Throw<PathTraversalException>();
    }

    // ── BuildCanonicalRelativePath ────────────────────────────────────────

    [Fact]
    public void BuildCanonicalRelativePath_ProducesSubmissionLeafPath()
    {
        var exhibitId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var path = AcceptedPathBuilder.BuildCanonicalRelativePath(
            "LOC001", "ROOM1", "20260101", 42, exhibitId, ".mp4");

        path.Should().Be($"loc001/room1/20260101/42/{exhibitId}.mp4");
    }

    [Fact]
    public void BuildCanonicalRelativePath_UsesExhibitIdNotOriginalName()
    {
        var exhibitId = Guid.NewGuid();
        var path = AcceptedPathBuilder.BuildCanonicalRelativePath(
            "loc", "room", "date", 1, exhibitId, ".pdf");

        path.Should().EndWith($"/{exhibitId}.pdf");
    }

    [Fact]
    public void BuildCanonicalRelativePath_Throws_WhenSubmissionIdNotPositive()
    {
        var act = () => AcceptedPathBuilder.BuildCanonicalRelativePath(
            "loc", "room", "date", 0, Guid.NewGuid(), ".mp4");
        act.Should().Throw<PathTraversalException>();
    }

    [Fact]
    public void BuildCanonicalRelativePath_Throws_WhenExhibitIdEmpty()
    {
        var act = () => AcceptedPathBuilder.BuildCanonicalRelativePath(
            "loc", "room", "date", 1, Guid.Empty, ".mp4");
        act.Should().Throw<PathTraversalException>();
    }

    // ── ResolveAndVerifyWithinRoot ────────────────────────────────────────

    [Fact]
    public void ResolveAndVerifyWithinRoot_ReturnsFullPath_ForValidRelative()
    {
        var root = Path.Combine(Path.GetTempPath(), "accepted-root");
        var resolved = AcceptedPathBuilder.ResolveAndVerifyWithinRoot(root, "loc/room/date/1/file.mp4");

        resolved.Should().StartWith(Path.GetFullPath(root));
        resolved.Should().EndWith($"1{Path.DirectorySeparatorChar}file.mp4");
    }

    [Theory]
    [InlineData("../escape.mp4")]
    [InlineData("../../etc/passwd")]
    [InlineData("loc/../../escape")]
    public void ResolveAndVerifyWithinRoot_Throws_OnTraversal(string relative)
    {
        var root = Path.Combine(Path.GetTempPath(), "accepted-root");
        var act = () => AcceptedPathBuilder.ResolveAndVerifyWithinRoot(root, relative);
        act.Should().Throw<PathTraversalException>();
    }

    [Fact]
    public void ResolveAndVerifyWithinRoot_Throws_OnSiblingPrefixMasquerade()
    {
        // "/tmp/accepted-root-evil" must not be accepted as under "/tmp/accepted-root".
        var root = Path.Combine(Path.GetTempPath(), "accepted-root");
        var act = () => AcceptedPathBuilder.ResolveAndVerifyWithinRoot(root, "../accepted-root-evil/x");
        act.Should().Throw<PathTraversalException>();
    }

    [Fact]
    public void ResolveAndVerifyWithinRoot_Throws_OnAbsolutePathEscape()
    {
        var root = Path.Combine(Path.GetTempPath(), "accepted-root");
        // An absolute path segment that resolves outside the root must be rejected.
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "x");
        var act = () => AcceptedPathBuilder.ResolveAndVerifyWithinRoot(root, outside);
        act.Should().Throw<PathTraversalException>();
    }
}
