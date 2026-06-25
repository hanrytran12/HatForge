namespace HatForge.Application.DTOs;

public record MaterialRequestDto(
    int Id,
    int OriginalDeliveryId,
    int BatchId,
    string BatchNumber,
    int WorkshopId,
    string WorkshopName,
    string Status,
    int CreatedByQCId,
    string CreatedByQCName,
    DateTime CreatedAt,
    int? ApprovedByLeadId,
    string? ApprovedByLeadName,
    DateTime? ApprovedAt,
    int? FulfilledByQCId,
    string? FulfilledByQCName,
    DateTime? FulfilledAt,
    int Round,
    List<MaterialRequestItemDto> Items
);

public record MaterialRequestItemDto(
    int Id,
    string MaterialName,
    string Unit,
    int ShortfallQuantity,
    int? ActualQuantity
);

public record ApproveMaterialRequestDto(int RequestId);

public record ConfirmMaterialRequestDto(
    int RequestId,
    List<ConfirmMaterialRequestItemDto> Items
);

public record ConfirmMaterialRequestItemDto(
    int ItemId,
    int ActualQuantity
);
