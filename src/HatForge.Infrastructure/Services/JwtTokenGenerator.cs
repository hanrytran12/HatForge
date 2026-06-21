using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HatForge.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HatForge.Infrastructure.Services;

public class JwtTokenGenerator : HatForge.Application.Interfaces.IJwtTokenGenerator
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenGenerator(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"] ?? "HatForgeSuperSecretKeyThatIsLongEnoughForHS256Algorithm!";
        _issuer = configuration["Jwt:Issuer"] ?? "HatForge";
        _audience = configuration["Jwt:Audience"] ?? "HatForge";
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
