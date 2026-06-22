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
    private static async Task<int> SeedChainAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        bool firstCompleted,
        bool withApprovedWork = true)
    {
        var batch = new Batch
        {
            BatchNumber = "B-200", HatModelId = 1, TargetQuantity = 100,
            Status = BatchStatus.InProduction, AssignedToLeadId = 1,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id, WorkshopId = 1, OrderIndex = 0, IsCompleted = firstCompleted,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10)
        });
        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id, WorkshopId = 2, OrderIndex = 1,
            StartDate = DateTime.UtcNow.AddDays(11), EndDate = DateTime.UtcNow.AddDays(20)
        });

        if (withApprovedWork)
            ctx.Works.Add(new Work
            {
                BatchId = batch.Id, WorkshopId = 1, StaffId = 2,
                Quantity = 10, Status = WorkStatus.Approved
            });

        await ctx.SaveChangesAsync();
        return batch.Id;
    }

    [Fact]
    public async Task CreateTransfer_ByQCOfWorkshop1_AutoDeterminesNextWorkshop()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        // QC id=3 belongs to workshop 1
        var result = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);

        Assert.Equal(nameof(TransferStatus.Pending), result.Status);
        Assert.Equal(1, result.FromWorkshopId);
        Assert.Equal(2, result.ToWorkshopId);
        Assert.Equal(3, result.CreatedByQCId);
    }

    [Fact]
    public async Task CreateTransfer_NoApprovedWork_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false, withApprovedWork: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3));
    }

    [Fact]
    public async Task CreateTransfer_SourceAlreadyCompleted_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: true);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3));
    }

    [Fact]
    public async Task CreateTransfer_ByQCOfOtherWorkshop_NotInBatch_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        // QC user id 5 belongs to workshop 3 which is NOT in the batch
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 5, workshopId: 3));
        await ctx.SaveChangesAsync();
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 5));
    }

    [Fact]
    public async Task ApproveTransfer_ByLead_SetsApproved()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);
        var result = await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 1);

        Assert.Equal(nameof(TransferStatus.Approved), result.Status);
        Assert.Equal(1, result.ApprovedByLeadId);
    }

    [Fact]
    public async Task ApproveTransfer_ByNonLead_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 3));
    }

    [Fact]
    public async Task ConfirmReceipt_ByNextWorkshopQC_MarksSourceCompleted_AndTransferred()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 5, workshopId: 2));
        await ctx.SaveChangesAsync();
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new TransferService(uow, new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);
        await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 1);
        var result = await service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Id), qcId: 5);

        Assert.Equal(nameof(TransferStatus.Transferred), result.Status);
        Assert.Equal(5, result.ConfirmedByQCId);

        var fromBw = await uow.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.True(fromBw!.IsCompleted);
    }

    [Fact]
    public async Task ConfirmReceipt_BeforeLeadApproval_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 5, workshopId: 2));
        await ctx.SaveChangesAsync();
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Id), qcId: 5));
    }

    [Fact]
    public async Task ConfirmReceipt_ByQCOfWrongWorkshop_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);
        await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 1);

        // QC id 3 belongs to workshop 1, not the destination workshop 2
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Id), qcId: 3));
    }
}
