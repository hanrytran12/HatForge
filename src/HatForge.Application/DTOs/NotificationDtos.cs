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

public record MaterialLowAlertPayload(
    int BatchId,
    int WorkshopId,
    decimal MaterialRemaining,
    decimal Threshold,
    List<MaterialLowAlertItemDto> Materials
);

public record MaterialLowAlertItemDto(
    string MaterialName,
    int ActualQuantity
);
