namespace HatForge.Domain.Entities;

public class MaterialRequestItem
{
    public int Id { get; set; }
    public int MaterialRequestId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int ShortfallQuantity { get; set; }
    public int? ActualQuantity { get; set; }

    public MaterialRequest MaterialRequest { get; set; } = null!;
}
