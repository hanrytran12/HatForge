namespace HatForge.Domain.Entities;

public class MaterialDeliveryItem
{
    public int Id { get; set; }
    public int MaterialDeliveryId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int PlannedQuantity { get; set; }
    public int ActualQuantity { get; set; }   // filled when QC confirms

    public MaterialDelivery MaterialDelivery { get; set; } = null!;
}
