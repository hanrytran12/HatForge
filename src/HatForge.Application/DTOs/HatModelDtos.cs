namespace HatForge.Application.DTOs;

public record HatModelDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    DateTime CreatedAt
);

public record CreateHatModelDto(
    string Name,
    string? Description
);

public record UpdateHatModelDto(
    string Name,
    string? Description
);
