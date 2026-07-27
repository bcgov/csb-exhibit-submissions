using CES.Business.Services;
using FluentAssertions;

namespace CES.Business.Tests.Services;

public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void HashPassword_ReturnsDifferentHash_EachCall()
    {
        var hash1 = _service.HashPassword("password123");
        var hash2 = _service.HashPassword("password123");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
    {
        var hash = _service.HashPassword("password123");

        _service.VerifyPassword("password123", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForWrongPassword()
    {
        var hash = _service.HashPassword("password123");

        _service.VerifyPassword("wrongpassword", hash).Should().BeFalse();
    }
}
