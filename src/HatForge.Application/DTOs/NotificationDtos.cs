namespace HatForge.Application.DTOs;

public record NotificationDto(
    int Id,
    string Type,
    string Title,
    string Message,
    string? Payload,
    bool IsRead,
    DateTime CreatedAt
);
