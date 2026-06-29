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
                Quantity = 10, Status = WorkStatus.Approved,
                PassedQuantity = 10
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

        Assert.False(result.IsFinalWorkshop);
        Assert.NotNull(result.Transfer);
        Assert.Equal(nameof(TransferStatus.Pending), result.Transfer!.Status);
        Assert.Equal(1, result.Transfer.FromWorkshopId);
        Assert.Equal(2, result.Transfer.ToWorkshopId);
        Assert.Equal(3, result.Transfer.CreatedByQCId);
        Assert.Equal(10, result.Transfer.ApprovedQuantity);
    }

    [Fact]
    public async Task CreateTransfer_IncludesUnrepairableRejectedWorkInQualityIssues()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        ctx.Works.Add(new Work
        {
            BatchId = batchId,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 3,
            Status = WorkStatus.Rejected,
            RejectionNotes = "Không thể sửa đường may bị lệch",
            UnrepairableQuantity = 3,
            ActualMaterialUsed = 3m,
            ReviewedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var result = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);

        var issue = Assert.Single(result.Transfer!.QualityIssues);
        Assert.Equal(3, issue.SubmittedQuantity);
        Assert.Equal(3, issue.UnrepairableQuantity);
        Assert.Equal("Không thể sửa đường may bị lệch", issue.RejectionNotes);
        Assert.Equal(3m, issue.ActualMaterialUsed);
    }

    [Fact]
    public async Task CreateTransfer_AllowsPartialPassedRejectedWork_WhenNoRepairableRemaining()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false, withApprovedWork: false);
        ctx.Works.Add(new Work
        {
            BatchId = batchId,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 500,
            Status = WorkStatus.Rejected,
            PassedQuantity = 250,
            RepairableQuantity = 0,
            UnrepairableQuantity = 250,
            RejectionNotes = "250 đạt, 250 hỏng không sửa được"
        });
        await ctx.SaveChangesAsync();
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var result = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);

        Assert.False(result.IsFinalWorkshop);
        Assert.NotNull(result.Transfer);
        Assert.Equal(250, result.Transfer!.ApprovedQuantity);
        var issue = Assert.Single(result.Transfer.QualityIssues);
        Assert.Equal(250, issue.PassedQuantity);
        Assert.Equal(250, issue.UnrepairableQuantity);
    }

    [Fact]
    public async Task CreateTransfer_BlocksWhenRepairableRejectedWorkRemains()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false, withApprovedWork: false);
        ctx.Works.Add(new Work
        {
            BatchId = batchId,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 500,
            Status = WorkStatus.Rejected,
            PassedQuantity = 250,
            RepairableQuantity = 250,
            UnrepairableQuantity = 0,
            RejectionNotes = "250 đạt, 250 cần sửa"
        });
        await ctx.SaveChangesAsync();
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3));
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
        var result = await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Transfer!.Id), leadId: 1);

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
            service.ApproveTransferAsync(new ApproveTransferDto(transfer.Transfer!.Id), leadId: 3));
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
        await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Transfer!.Id), leadId: 1);
        var result = await service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Transfer!.Id, 8, 2, "2 lỗi"), qcId: 5);

        Assert.Equal(nameof(TransferStatus.Transferred), result.Status);
        Assert.Equal(5, result.ConfirmedByQCId);
        Assert.Equal(8, result.ReceivedUsableQuantity);
        Assert.Equal(2, result.ReceivedDefectiveQuantity);
        Assert.Equal(2, result.ReceiptDiscrepancyQuantity);
        Assert.Equal("2 lỗi", result.ReceiptInspectionNotes);

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
            service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Transfer!.Id, 10, 0), qcId: 5));
    }

    [Fact]
    public async Task ConfirmReceipt_ByQCOfWrongWorkshop_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);
        await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Transfer!.Id), leadId: 1);

        // QC id 3 belongs to workshop 1, not the destination workshop 2
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Transfer!.Id, 10, 0), qcId: 3));
    }

    [Fact]
    public async Task ConfirmReceipt_WhenReceiptQuantitiesDoNotMatchApprovedQuantity_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 5, workshopId: 2));
        await ctx.SaveChangesAsync();
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);
        await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Transfer!.Id), leadId: 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Transfer!.Id, 7, 2), qcId: 5));
    }

    [Fact]
    public async Task ConfirmReceipt_AllDefective_AllowsZeroUsable()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 5, workshopId: 2));
        await ctx.SaveChangesAsync();
        var batchId = await SeedChainAsync(ctx, firstCompleted: false);
        var service = new TransferService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var transfer = await service.CreateTransferRequestAsync(new CreateTransferDto(batchId), qcId: 3);
        await service.ApproveTransferAsync(new ApproveTransferDto(transfer.Transfer!.Id), leadId: 1);
        var result = await service.ConfirmReceiptAsync(new ConfirmReceiptDto(transfer.Transfer!.Id, 0, 10), qcId: 5);

        Assert.Equal(nameof(TransferStatus.Transferred), result.Status);
        Assert.Equal(0, result.ReceivedUsableQuantity);
        Assert.Equal(10, result.ReceivedDefectiveQuantity);
        Assert.Equal(10, result.ReceiptDiscrepancyQuantity);
    }
}
