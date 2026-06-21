using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class WorkServiceTests
{
    private static async Task<int> SeedBatchWithWorkshopAsync(HatForge.Infrastructure.Data.AppDbContext ctx, bool requiresMaterials = false, bool materialsReceived = false)
    {
        var batch = new Batch { BatchNumber = "B-100", HatModelId = 1, TargetQuantity = 100, Status = BatchStatus.InProduction, AssignedToLeadId = 1 };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.Workshops.First(w => w.Id == 1).RequiresMaterials = requiresMaterials;
        ctx.BatchWorkshops.Add(new BatchWorkshop { BatchId = batch.Id, WorkshopId = 1, OrderIndex = 0, MaterialsReceived = materialsReceived });
        await ctx.SaveChangesAsync();
        return batch.Id;
    }

    [Fact]
    public async Task SubmitWork_ValidStaff_CreatesSubmittedWork()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var result = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, "/uploads/p.jpg"), staffId: 2);

        Assert.Equal(nameof(WorkStatus.Submitted), result.Status);
        Assert.Equal(10, result.Quantity);
    }

    [Fact]
    public async Task SubmitWork_NonStaff_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, "/uploads/p.jpg"), staffId: 1));
    }

    [Fact]
    public async Task SubmitWork_MaterialsNotReceived_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx, requiresMaterials: true, materialsReceived: false);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, "/uploads/p.jpg"), staffId: 2));
    }

    [Fact]
    public async Task ApproveWork_ByQc_SetsApproved()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, "/uploads/p.jpg"), staffId: 2);
        var result = await service.ApproveWorkAsync(work.Id, qcId: 3);

        Assert.Equal(nameof(WorkStatus.Approved), result.Status);
        Assert.Equal(3, result.ReviewedByQCId);
    }

    [Fact]
    public async Task RejectWork_ByQc_SetsRejectedWithReason()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, "/uploads/p.jpg"), staffId: 2);
        var result = await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Craftsmanship", "Loose stitching"), qcId: 3);

        Assert.Equal(nameof(WorkStatus.Rejected), result.Status);
        Assert.Equal("Loose stitching", result.RejectionNotes);
    }

    [Fact]
    public async Task ApproveWork_AlreadyApproved_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, "/uploads/p.jpg"), staffId: 2);
        await service.ApproveWorkAsync(work.Id, qcId: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApproveWorkAsync(work.Id, qcId: 3));
    }
}
