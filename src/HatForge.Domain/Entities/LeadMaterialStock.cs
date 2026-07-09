namespace HatForge.Domain.Entities;

public class LeadMaterialStock
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string NormalizedMaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Lead { get; set; } = null!;
    public ICollection<LeadMaterialStockTransaction> Transactions { get; set; } = new List<LeadMaterialStockTransaction>();
}
