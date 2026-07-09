using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class LeadMaterialStockTransaction
{
    public int Id { get; set; }
    public int LeadMaterialStockId { get; set; }
    public int LeadId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string NormalizedMaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityDelta { get; set; }
    public decimal QuantityAfter { get; set; }
    public LeadMaterialStockTransactionType Type { get; set; }
    public int? BatchId { get; set; }
    public int? MaterialDeliveryId { get; set; }
    public int? MaterialRequestId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public LeadMaterialStock LeadMaterialStock { get; set; } = null!;
    public User Lead { get; set; } = null!;
    public Batch? Batch { get; set; }
    public MaterialDelivery? MaterialDelivery { get; set; }
    public MaterialRequest? MaterialRequest { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
