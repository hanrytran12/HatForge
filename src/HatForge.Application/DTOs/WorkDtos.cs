namespace HatForge.Application.DTOs;

public record WorkDto(
    int Id,
    int BatchId,
    int WorkshopId,
    string WorkshopName,
    int StaffId,
    string StaffName,
    int Quantity,
    string PhotoUrl,
    DateTime SubmittedDate,
    string Status,
    string? RejectionReason,
    string? RejectionNotes,
    int? ReviewedByQCId,
    DateTime? ReviewedAt
);

public record SubmitWorkDto(int BatchId, int WorkshopId, int Quantity, string PhotoUrl);

public record ApproveWorkDto(int WorkId);

public record RejectWorkDto(int WorkId, string RejectionReason, string RejectionNotes);
