namespace CES.API.Authentication
{
    public interface ITokenService
    {
        string GenerateToken(string username, string role = "User");
    }
}