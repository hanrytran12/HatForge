using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class Work
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int WorkshopId { get; set; }
    public int StaffId { get; set; }
    public int Quantity { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
    public WorkStatus Status { get; set; } = WorkStatus.Submitted;

    public RejectionReasonType? RejectionReason { get; set; }
    public string? RejectionNotes { get; set; }
    public int? ReviewedByQCId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public Batch Batch { get; set; } = null!;
    public Workshop Workshop { get; set; } = null!;
    public User Staff { get; set; } = null!;
    public User? ReviewedByQC { get; set; }
}
