using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class LeadInventoryServiceTests
{
    [Fact]
    public async Task StockIn_NewMaterial_CreatesStockAndLedger()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = new LeadInventoryService(TestDataFactory.CreateUnitOfWork(ctx));

        var result = await service.StockInAsync(
            new StockInLeadMaterialDto("Wool Felt", "M", 250m, "Initial stock"),
            leadId: 1);

        Assert.Equal("Wool Felt", result.MaterialName);
        Assert.Equal("m", result.Unit);
        Assert.Equal(250m, result.QuantityOnHand);

        var transactions = await service.GetTransactionsAsync(1);
        var tx = Assert.Single(transactions);
        Assert.Equal(nameof(LeadMaterialStockTransactionType.StockIn), tx.Type);
        Assert.Equal(250m, tx.QuantityDelta);
        Assert.Equal(250m, tx.QuantityAfter);
    }

    [Fact]
    public async Task StockIn_ExistingMaterial_AddsToStock()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await TestDataFactory.SeedLeadStockAsync(ctx, 1, "Wool Felt", "m", 100m);
        var service = new LeadInventoryService(TestDataFactory.CreateUnitOfWork(ctx));

        var result = await service.StockInAsync(
            new StockInLeadMaterialDto(" wool felt ", "M", 50m, null),
            leadId: 1);

        Assert.Equal(150m, result.QuantityOnHand);
        Assert.Single(await service.GetStocksAsync(1));
    }

    [Fact]
    public async Task Adjust_SetsAbsoluteQuantityAndWritesDelta()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await TestDataFactory.SeedLeadStockAsync(ctx, 1, "Thread", "roll", 20m);
        var service = new LeadInventoryService(TestDataFactory.CreateUnitOfWork(ctx));

        var result = await service.AdjustAsync(
            new AdjustLeadMaterialStockDto("Thread", "roll", 14m, "Physical count"),
            leadId: 1);

        Assert.Equal(14m, result.QuantityOnHand);
        var tx = Assert.Single(await service.GetTransactionsAsync(1));
        Assert.Equal(nameof(LeadMaterialStockTransactionType.Adjustment), tx.Type);
        Assert.Equal(-6m, tx.QuantityDelta);
        Assert.Equal(14m, tx.QuantityAfter);
    }

    [Fact]
    public async Task StockIn_ByNonLead_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = new LeadInventoryService(TestDataFactory.CreateUnitOfWork(ctx));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.StockInAsync(new StockInLeadMaterialDto("Wool Felt", "m", 10m, null), leadId: 2));
    }
}
