namespace HatForge.Application.DTOs;

public record BatchDto(
    int Id,
    string BatchNumber,
    int HatModelId,
    string HatModelCode,
    string HatModelName,
    string Status,
    int? AssignedToLeadId,
    string? AssignedToLeadName,
    int TargetQuantity,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int? CompletedQuantity,
    List<WorkshopInBatchDto> Workshops
);

public record WorkshopInBatchDto(
    int WorkshopId,
    string Name,
    int OrderIndex,
    bool RequiresMaterials,
    bool MaterialsReceived,
    bool IsCompleted,
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialMaterialQty,
    decimal MaterialUsed,
    decimal MaterialRemaining,
    decimal EstimatedMetersPerUnit
);

public record CreateBatchDto(
    int HatModelId,
    int TargetQuantity,
    DateTime StartDate,
    DateTime EndDate,
    int AssignToLeadId
);

public record PlanBatchDto(List<WorkshopPlanItemDto> Workshops);

public record WorkshopPlanItemDto(
    int WorkshopId,
    int OrderIndex,
    bool RequiresMaterials,
    DateTime StartDate,
    DateTime EndDate,
    DateTime? MaterialDeliveryDate,          // required when RequiresMaterials = true
    List<MaterialItemDto>? MaterialItems,    // required when RequiresMaterials = true
    decimal EstimatedMetersPerUnit = 0m     // required (>0) when RequiresMaterials = true
);

public record MaterialItemDto(
    string MaterialName,
    int PlannedQuantity,
    string Unit = "m"
);

public record BatchListDto(
    int Id,
    string BatchNumber,
    string HatModelCode,
    string HatModelName,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt
);

public record BatchFinalSummaryDto(
    int BatchId,
    string BatchNumber,
    string Status,
    int TargetQuantity,
    int? CompletedQuantity,
    List<FinalSummaryWorkshopDto> Workshops,
    FinalSummaryTransferCountsDto Transfers,
    FinalSummaryMaterialRequestCountsDto MaterialRequests
);

public record FinalSummaryWorkshopDto(
    int WorkshopId,
    string WorkshopName,
    int OrderIndex,
    bool IsCompleted,
    bool RequiresMaterials,
    bool MaterialsReceived,
    FinalSummaryWorkCountsDto Works,
    FinalSummaryMaterialUsageDto Materials
);

public record FinalSummaryWorkCountsDto(
    int Submitted,
    int Approved,
    int Rejected,
    int ApprovedQuantity
);

public record FinalSummaryMaterialUsageDto(
    decimal InitialQty,
    decimal Used,
    decimal Remaining
);

public record FinalSummaryTransferCountsDto(
    int Pending,
    int Approved,
    int Transferred,
    int Total
);

public record FinalSummaryMaterialRequestCountsDto(
    int Pending,
    int Approved,
    int Fulfilled,
    int Total
);
