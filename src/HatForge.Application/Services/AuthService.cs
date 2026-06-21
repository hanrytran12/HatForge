using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUnitOfWork unitOfWork, IJwtTokenGenerator tokenGenerator, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Email == dto.Email)
            ?? throw new ForbiddenException("Invalid credentials");

        if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
            throw new ForbiddenException("Invalid credentials");

        var token = _tokenGenerator.GenerateToken(user);
        return new AuthResponseDto(token, user.Id, user.Name, user.Email, user.Role.ToString(), user.WorkshopId);
    }

    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        var existing = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
        if (existing != null)
            throw new BusinessRuleException("Email already registered");

        var user = new User
        {
            Email = dto.Email,
            Name = dto.Name,
            Role = dto.Role,
            WorkshopId = dto.WorkshopId,
            PasswordHash = _passwordHasher.Hash(dto.Password)
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new UserDto(user.Id, user.Email, user.Name, user.Role.ToString(), user.WorkshopId);
    }
}
