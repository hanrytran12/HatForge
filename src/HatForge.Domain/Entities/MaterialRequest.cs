using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class MaterialRequest
{
    public int Id { get; set; }
    public int OriginalDeliveryId { get; set; }
    public int BatchId { get; set; }
    public MaterialRequestStatus Status { get; set; } = MaterialRequestStatus.Pending;
    public int CreatedByQCId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? ApprovedByLeadId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? FulfilledByQCId { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public int Round { get; set; } = 1;

    public MaterialDelivery OriginalDelivery { get; set; } = null!;
    public Batch Batch { get; set; } = null!;
    public User CreatedByQC { get; set; } = null!;
    public User? ApprovedByLead { get; set; }
    public User? FulfilledByQC { get; set; }
    public ICollection<MaterialRequestItem> Items { get; set; } = new List<MaterialRequestItem>();
}