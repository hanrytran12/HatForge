namespace HatForge.Application.DTOs;

public record LeadMaterialStockDto(
    int Id,
    int LeadId,
    string MaterialName,
    string Unit,
    decimal QuantityOnHand,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record LeadMaterialStockTransactionDto(
    int Id,
    int LeadMaterialStockId,
    int LeadId,
    string MaterialName,
    string Unit,
    decimal QuantityDelta,
    decimal QuantityAfter,
    string Type,
    int? BatchId,
    int? MaterialDeliveryId,
    int? MaterialRequestId,
    int CreatedByUserId,
    DateTime CreatedAt,
    string? Notes
);

public record StockInLeadMaterialDto(
    string MaterialName,
    string Unit,
    decimal Quantity,
    string? Notes
);

public record AdjustLeadMaterialStockDto(
    string MaterialName,
    string Unit,
    decimal NewQuantityOnHand,
    string Reason
);
