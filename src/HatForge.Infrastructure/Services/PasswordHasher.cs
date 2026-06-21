namespace HatForge.Infrastructure.Services;

public class PasswordHasher : HatForge.Application.Interfaces.IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
