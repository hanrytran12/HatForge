namespace HatForge.Application.DTOs;

public record TransferRequestDto(
    int Id,
    int BatchId,
    string BatchNumber,
    int FromWorkshopId,
    string FromWorkshopName,
    int ToWorkshopId,
    string ToWorkshopName,
    DateTime CreatedAt,
    int? ApprovedByLeadId,
    DateTime? ApprovedAt,
    string Status
);

public record CreateTransferDto(int BatchId, int FromWorkshopId, int ToWorkshopId);

public record ApproveTransferDto(int TransferId);

public record MaterialDeliveryDto(
    int Id,
    int BatchId,
    int WorkshopId,
    string WorkshopName,
    DateTime ScheduledDate,
    DateTime? DeliveredDate,
    int DeliveredQuantity,
    bool IsConfirmed,
    string Status
);

public record CreateMaterialDeliveryDto(int BatchId, int WorkshopId, DateTime ScheduledDate, int DeliveredQuantity);

public record ConfirmMaterialDeliveryDto(int DeliveryId, int DeliveredQuantity);
