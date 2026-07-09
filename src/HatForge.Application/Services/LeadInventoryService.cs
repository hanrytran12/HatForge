using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class LeadInventoryService : ILeadInventoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public LeadInventoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<LeadMaterialStockDto>> GetStocksAsync(int leadId)
    {
        await EnsureLeadAsync(leadId);

        var stocks = await _unitOfWork.LeadMaterialStocks.FindAsync(x => x.LeadId == leadId);
        return stocks
            .OrderBy(x => x.MaterialName)
            .ThenBy(x => x.Unit)
            .Select(MapStock)
            .ToList();
    }

    public async Task<IReadOnlyList<LeadMaterialStockTransactionDto>> GetTransactionsAsync(int leadId)
    {
        await EnsureLeadAsync(leadId);

        var transactions = await _unitOfWork.LeadMaterialStockTransactions.FindAsync(x => x.LeadId == leadId);
        return transactions
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(MapTransaction)
            .ToList();
    }

    public async Task<LeadMaterialStockDto> StockInAsync(StockInLeadMaterialDto dto, int leadId)
    {
        await EnsureLeadAsync(leadId);

        if (dto.Quantity <= 0)
            throw new BusinessRuleException("Stock-in quantity must be greater than 0");

        var materialName = CleanMaterialName(dto.MaterialName);
        var normalizedMaterialName = NormalizeMaterialName(materialName);
        var unit = NormalizeUnit(dto.Unit);

        LeadMaterialStock? stock = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            stock = await GetOrCreateStockAsync(leadId, materialName, normalizedMaterialName, unit);
            stock.QuantityOnHand += dto.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.LeadMaterialStocks.Update(stock);

            await AddTransactionAsync(
                stock,
                dto.Quantity,
                LeadMaterialStockTransactionType.StockIn,
                createdByUserId: leadId,
                notes: string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim());

            await _unitOfWork.SaveChangesAsync();
        });

        return MapStock(stock!);
    }

    public async Task<LeadMaterialStockDto> AdjustAsync(AdjustLeadMaterialStockDto dto, int leadId)
    {
        await EnsureLeadAsync(leadId);

        if (dto.NewQuantityOnHand < 0)
            throw new BusinessRuleException("Adjusted quantity must be greater than or equal to 0");

        var materialName = CleanMaterialName(dto.MaterialName);
        var normalizedMaterialName = NormalizeMaterialName(materialName);
        var unit = NormalizeUnit(dto.Unit);
        LeadMaterialStock? stock = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            stock = await GetOrCreateStockAsync(leadId, materialName, normalizedMaterialName, unit);
            var delta = dto.NewQuantityOnHand - stock.QuantityOnHand;
            stock.QuantityOnHand = dto.NewQuantityOnHand;
            stock.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.LeadMaterialStocks.Update(stock);

            await AddTransactionAsync(
                stock,
                delta,
                LeadMaterialStockTransactionType.Adjustment,
                createdByUserId: leadId,
                notes: dto.Reason.Trim());

            await _unitOfWork.SaveChangesAsync();
        });

        return MapStock(stock!);
    }

    public static string CleanMaterialName(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
            throw new BusinessRuleException("Material name is required");
        return materialName.Trim();
    }

    public static string NormalizeMaterialName(string materialName) =>
        CleanMaterialName(materialName).ToUpperInvariant();

    public static string NormalizeUnit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            throw new BusinessRuleException("Unit is required");
        return unit.Trim().ToLowerInvariant();
    }

    private async Task<User> EnsureLeadAsync(int leadId)
    {
        var lead = await _unitOfWork.Users.GetByIdAsync(leadId)
            ?? throw new NotFoundException("Lead not found");
        if (lead.Role != UserRole.Lead)
            throw new ForbiddenException("Only Lead users can manage lead inventory");
        return lead;
    }

    private async Task<LeadMaterialStock> GetOrCreateStockAsync(
        int leadId,
        string materialName,
        string normalizedMaterialName,
        string unit)
    {
        var stock = await _unitOfWork.LeadMaterialStocks.FirstOrDefaultAsync(x =>
            x.LeadId == leadId
            && x.NormalizedMaterialName == normalizedMaterialName
            && x.Unit == unit);

        if (stock != null)
            return stock;

        stock = new LeadMaterialStock
        {
            LeadId = leadId,
            MaterialName = materialName,
            NormalizedMaterialName = normalizedMaterialName,
            Unit = unit,
            QuantityOnHand = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _unitOfWork.LeadMaterialStocks.AddAsync(stock);
        await _unitOfWork.SaveChangesAsync();
        return stock;
    }

    private async Task AddTransactionAsync(
        LeadMaterialStock stock,
        decimal quantityDelta,
        LeadMaterialStockTransactionType type,
        int createdByUserId,
        string? notes = null)
    {
        await _unitOfWork.LeadMaterialStockTransactions.AddAsync(new LeadMaterialStockTransaction
        {
            LeadMaterialStockId = stock.Id,
            LeadId = stock.LeadId,
            MaterialName = stock.MaterialName,
            NormalizedMaterialName = stock.NormalizedMaterialName,
            Unit = stock.Unit,
            QuantityDelta = quantityDelta,
            QuantityAfter = stock.QuantityOnHand,
            Type = type,
            CreatedByUserId = createdByUserId,
            Notes = notes
        });
    }

    private static LeadMaterialStockDto MapStock(LeadMaterialStock stock) => new(
        stock.Id,
        stock.LeadId,
        stock.MaterialName,
        stock.Unit,
        stock.QuantityOnHand,
        stock.CreatedAt,
        stock.UpdatedAt);

    private static LeadMaterialStockTransactionDto MapTransaction(LeadMaterialStockTransaction tx) => new(
        tx.Id,
        tx.LeadMaterialStockId,
        tx.LeadId,
        tx.MaterialName,
        tx.Unit,
        tx.QuantityDelta,
        tx.QuantityAfter,
        tx.Type.ToString(),
        tx.BatchId,
        tx.MaterialDeliveryId,
        tx.MaterialRequestId,
        tx.CreatedByUserId,
        tx.CreatedAt,
        tx.Notes);
}
