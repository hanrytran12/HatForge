namespace HatForge.Application.DTOs;

public record WorkDto(
    int Id,
    int BatchId,
    int WorkshopId,
    string WorkshopName,
    int StaffId,
    string StaffName,
    int Quantity,
    List<string> PhotoUrls,
    List<string> RejectionPhotoUrls,
    DateTime SubmittedDate,
    string Status,
    string? RejectionNotes,
    int? ReviewedByQCId,
    DateTime? ReviewedAt
);

public record SubmitWorkDto(int BatchId, int WorkshopId, int Quantity, List<string> PhotoUrls);

public record ApproveWorkDto(int WorkId);

public record RejectWorkDto(int WorkId, string RejectionNotes, List<string> PhotoUrls);
