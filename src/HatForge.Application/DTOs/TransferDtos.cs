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
    bool IsConfirmed,
    string Status,
    List<MaterialDeliveryItemDto> Items
);

public record MaterialDeliveryItemDto(
    int Id,
    string MaterialName,
    int PlannedQuantity,
    int ActualQuantity
);

public record ConfirmMaterialDeliveryDto(
    int DeliveryId,
    List<ConfirmMaterialItemDto> Items  // actual quantities per material
);

public record ConfirmMaterialItemDto(
    int ItemId,
    int ActualQuantity
);
