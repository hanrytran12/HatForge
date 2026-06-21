namespace HatForge.Application.DTOs;

public record BatchDto(
    int Id,
    string BatchNumber,
    int HatModelId,
    string HatModelName,
    string Status,
    int? AssignedToLeadId,
    string? AssignedToLeadName,
    int TargetQuantity,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt,
    DateTime? CompletedAt,
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
    DateTime EndDate
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
    List<MaterialItemDto>? MaterialItems     // required when RequiresMaterials = true
);

public record MaterialItemDto(
    string MaterialName,
    int PlannedQuantity
);

public record BatchListDto(
    int Id,
    string BatchNumber,
    string HatModelName,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt
);
