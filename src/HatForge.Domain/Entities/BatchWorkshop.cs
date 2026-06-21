namespace HatForge.Domain.Entities;

public class BatchWorkshop
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int WorkshopId { get; set; }
    public int OrderIndex { get; set; }
    public bool RequiresMaterials { get; set; }
    public bool MaterialsReceived { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Batch Batch { get; set; } = null!;
    public Workshop Workshop { get; set; } = null!;
}
