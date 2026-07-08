using HatForge.Domain.Enums;

namespace HatForge.Application.DTOs;

public record LoginDto(string Email, string Password);

public record RegisterDto(string Email, string Password, string Name, UserRole Role, int? WorkshopId);

public record AuthResponseDto(string Token, int UserId, string Name, string Email, string Role, int? WorkshopId);

public record UserDto(int Id, string Email, string Name, string Role, int? WorkshopId, string? WorkshopName, bool IsActive);
