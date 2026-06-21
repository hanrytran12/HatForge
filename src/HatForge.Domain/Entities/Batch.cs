using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class Batch
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int HatModelId { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Created;
    public int? AssignedToLeadId { get; set; }
    public int TargetQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public HatModel HatModel { get; set; } = null!;
    public User? AssignedToLead { get; set; }
    public ICollection<BatchWorkshop> BatchWorkshops { get; set; } = new List<BatchWorkshop>();
    public ICollection<Work> Works { get; set; } = new List<Work>();
    public ICollection<TransferRequest> TransferRequests { get; set; } = new List<TransferRequest>();
}
