using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class MaterialDelivery
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int WorkshopId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public bool IsConfirmed { get; set; }
    public int? ConfirmedByQCId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public MaterialDeliveryStatus Status { get; set; } = MaterialDeliveryStatus.Scheduled;

    public Batch Batch { get; set; } = null!;
    public Workshop Workshop { get; set; } = null!;
    public ICollection<MaterialDeliveryItem> Items { get; set; } = new List<MaterialDeliveryItem>();
}
