using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithInactiveUser_ThrowsUnauthorized()
    {
        using var ctx = TestDataFactory.CreateContext();
        var hasher = new TestPasswordHasher();
        ctx.Users.Add(new User
        {
            Email = "inactive@hf.com",
            Name = "Inactive",
            Role = UserRole.Staff,
            WorkshopId = 1,
            PasswordHash = hasher.Hash("Staff123!"),
            IsActive = false
        });
        await ctx.SaveChangesAsync();
        var service = new AuthService(TestDataFactory.CreateUnitOfWork(ctx), new TestJwtTokenGenerator(), hasher);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginDto("inactive@hf.com", "Staff123!")));
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    private sealed class TestJwtTokenGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(User user) => $"token:{user.Id}";
    }
}
