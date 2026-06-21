using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class TransferServiceTests
{
    private static async Task<int> SeedChainAsync(HatForge.Infrastructure.Data.AppDbContext ctx, bool firstCompleted)
    {
        var batch = new Batch { BatchNumber = "B-200", HatModelId = 1, TargetQuantity = 100, Status = BatchStatus.ReadyForTransfer, AssignedToLeadId = 1 };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop { BatchId = batch.Id, WorkshopId = 1, OrderIndex = 0, IsCompleted = firstCompleted });
        ctx.BatchWorkshops.Add(new BatchWorkshop { BatchId = batch.Id, WorkshopId = 2, OrderIndex = 1 });
        await ctx.SaveChangesAsync();
        return batch.Id;
    }

    [Fact]
    public async Task CreateTransfer_SourceCompleted_NextInChain_Succeeds()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: true);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var result = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId, 1, 2));

        Assert.Equal(nameof(TransferStatus.Pending), result.Status);
    }

    [Fact]
    public async Task CreateTransfer_SourceNotCompleted_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateTransferRequestAsync(new CreateTransferDto(batchId, 1, 2)));
    }

    [Fact]
    public async Task ApproveTransfer_ByLead_SetsTransferred()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: true);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId, 1, 2));
        var result = await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 1);

        Assert.Equal(nameof(TransferStatus.Transferred), result.Status);
        Assert.Equal(1, result.ApprovedByLeadId);
    }

    [Fact]
    public async Task ApproveTransfer_ByNonLead_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: true);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId, 1, 2));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 3));
    }
}
