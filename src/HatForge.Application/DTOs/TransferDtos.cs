namespace HatForge.Application.DTOs;

public record TransferRequestDto(
    int Id,
    int BatchId,
    string BatchNumber,
    int FromWorkshopId,
    string FromWorkshopName,
    int ToWorkshopId,
    string ToWorkshopName,
    int ApprovedQuantity,
    int? ReceivedUsableQuantity,
    int? ReceivedDefectiveQuantity,
    int? ReceiptDiscrepancyQuantity,
    string? ReceiptInspectionNotes,
    DateTime CreatedAt,
    int? CreatedByQCId,
    int? ApprovedByLeadId,
    DateTime? ApprovedAt,
    int? ConfirmedByQCId,
    DateTime? ConfirmedAt,
    string Status,
    List<TransferQualityIssueDto> QualityIssues
);

public record TransferQualityIssueDto(
    int WorkId,
    int StaffId,
    string StaffName,
    int SubmittedQuantity,
    int PassedQuantity,
    int RepairableQuantity,
    int UnrepairableQuantity,
    string RejectionNotes,
    decimal? ActualMaterialUsed,
    DateTime? ReviewedAt,
    List<string> RejectionPhotoUrls
);

public record CreateTransferDto(int BatchId);

public record FinalReviewDto(int BatchId, string BatchNumber, string Status);

public record CreateTransferResultDto(
    bool IsFinalWorkshop,
    TransferRequestDto? Transfer,   // null when IsFinalWorkshop = true
    string BatchStatus              // current batch status after the operation
);

public record ApproveTransferDto(int TransferId);

public record ConfirmReceiptDto(
    int TransferId,
    int ReceivedUsableQuantity,
    int ReceivedDefectiveQuantity,
    string? ReceiptInspectionNotes = null);

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
    int ActualQuantity,
    string Unit = "m"
);

public record ConfirmMaterialDeliveryDto(
    int DeliveryId,
    List<ConfirmMaterialItemDto> Items  // actual quantities per material
);

public record ConfirmMaterialItemDto(
    int ItemId,
    int ActualQuantity
);
