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
    DateTime CreatedAt,
    DateTime? CompletedAt,
    List<WorkshopInBatchDto> Workshops
);

public record WorkshopInBatchDto(int WorkshopId, string Name, int OrderIndex, bool MaterialsReceived, bool IsCompleted);

public record CreateBatchDto(
    string BatchNumber,
    int HatModelId,
    int TargetQuantity,
    List<int> WorkshopIds,
    int AssignToLeadId
);

public record AssignLeadDto(int LeadId);

public record BatchListDto(int Id, string BatchNumber, string HatModelName, string Status, DateTime CreatedAt);
