using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class TransferRequest
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int FromWorkshopId { get; set; }
    public int ToWorkshopId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByQCId { get; set; }
    public int? ApprovedByLeadId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ConfirmedByQCId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    public Batch Batch { get; set; } = null!;
    public Workshop FromWorkshop { get; set; } = null!;
    public Workshop ToWorkshop { get; set; } = null!;
    public User? CreatedByQC { get; set; }
    public User? ApprovedByLead { get; set; }
    public User? ConfirmedByQC { get; set; }
}
