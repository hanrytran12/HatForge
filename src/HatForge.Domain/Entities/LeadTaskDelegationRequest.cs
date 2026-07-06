using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class LeadTaskDelegationRequest
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int? MaterialDeliveryId { get; set; }
    public int? TransferRequestId { get; set; }
    public LeadTaskDelegationType Type { get; set; }
    public LeadTaskDelegationStatus Status { get; set; } = LeadTaskDelegationStatus.PendingAdminApproval;
    public int RequestedByLeadId { get; set; }
    public int AssignedTransportQcId { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public int? CompletedByTransportQcId { get; set; }
    public string? Reason { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Batch? Batch { get; set; }
    public MaterialDelivery? MaterialDelivery { get; set; }
    public TransferRequest? TransferRequest { get; set; }
    public User? RequestedByLead { get; set; }
    public User? AssignedTransportQc { get; set; }
    public User? ReviewedByAdmin { get; set; }
    public User? CompletedByTransportQc { get; set; }
}
