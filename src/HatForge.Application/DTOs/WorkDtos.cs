namespace HatForge.Application.DTOs;

public record WorkDto(
    int Id,
    int BatchId,
    int WorkshopId,
    string WorkshopName,
    int StaffId,
    string StaffName,
    int Quantity,
    bool IsRework,
    List<string> PhotoUrls,
    List<string> RejectionPhotoUrls,
    DateTime SubmittedDate,
    string Status,
    string? RejectionNotes,
    int PassedQuantity,
    int RepairableQuantity,
    int UnrepairableQuantity,
    int? ReviewedByQCId,
    DateTime? ReviewedAt,
    decimal? ActualMaterialUsed,
    decimal? EstimatedMaterialUsed
);

public record SubmitWorkDto(int BatchId, int WorkshopId, int Quantity, bool IsRework, List<string> PhotoUrls);

public record ApproveWorkDto(int WorkId, decimal ActualMaterialUsed, string? Notes);

public record RejectWorkDto(
    int WorkId,
    string RejectionNotes,
    int PassedQuantity,
    int RepairableQuantity,
    int UnrepairableQuantity,
    decimal ActualMaterialUsed,
    List<string> PhotoUrls);
