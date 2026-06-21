using HatForge.Domain.Entities;

namespace HatForge.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
