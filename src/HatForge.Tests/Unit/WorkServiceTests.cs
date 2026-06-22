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
    private static async Task<int> SeedBatchWithWorkshopAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        bool requiresMaterials = false,
        bool materialsReceived = false)
    {
        var batch = new Batch
        {
            BatchNumber = "B-100", HatModelId = 1, TargetQuantity = 100,
            Status = BatchStatus.InProduction, AssignedToLeadId = 1,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id, WorkshopId = 1, OrderIndex = 0,
            RequiresMaterials = requiresMaterials, MaterialsReceived = materialsReceived,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10)
        });
        await ctx.SaveChangesAsync();
        return batch.Id;
    }

    private static async Task<int> SeedBatchWithTwoWorkshopsAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        bool withTransfer = false)
    {
        var batch = new Batch
        {
            BatchNumber = "B-200", HatModelId = 1, TargetQuantity = 100,
            Status = BatchStatus.InProduction, AssignedToLeadId = 1,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.AddRange(
            new BatchWorkshop
            {
                BatchId = batch.Id, WorkshopId = 1, OrderIndex = 0,
                IsCompleted = true, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10)
            },
            new BatchWorkshop
            {
                BatchId = batch.Id, WorkshopId = 2, OrderIndex = 1,
                StartDate = DateTime.UtcNow.AddDays(11), EndDate = DateTime.UtcNow.AddDays(20)
            });
        await ctx.SaveChangesAsync();

        if (withTransfer)
        {
            ctx.TransferRequests.Add(new TransferRequest
            {
                BatchId = batch.Id,
                FromWorkshopId = 1,
                ToWorkshopId = 2,
                Status = TransferStatus.Transferred,
                ApprovedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        return batch.Id;
    }

    [Fact]
    public async Task SubmitWork_ValidStaff_FirstWorkshop_Succeeds()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var result = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);

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

        // Lead (id=1) tries to submit
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 1));
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
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2));
    }

    [Fact]
    public async Task SubmitWork_SecondWorkshop_WithoutTransfer_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithTwoWorkshopsAsync(ctx, withTransfer: false);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        // Staff of workshop 2 (staffId=2 belongs to workshop 1 in seed, use QC of workshop 2 won't work either)
        // Use a staff user assigned to workshop 2
        ctx.Users.Add(new HatForge.Domain.Entities.User
        {
            Id = 10, Email = "staff2@hf.com", Name = "Staff2",
            Role = UserRole.Staff, WorkshopId = 2, PasswordHash = "x"
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 2, 10, new List<string> { "/uploads/p.jpg" }), staffId: 10));
    }

    [Fact]
    public async Task SubmitWork_SecondWorkshop_WithTransfer_Succeeds()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithTwoWorkshopsAsync(ctx, withTransfer: true);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        ctx.Users.Add(new HatForge.Domain.Entities.User
        {
            Id = 10, Email = "staff2@hf.com", Name = "Staff2",
            Role = UserRole.Staff, WorkshopId = 2, PasswordHash = "x"
        });
        await ctx.SaveChangesAsync();

        var result = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 2, 10, new List<string> { "/uploads/p.jpg" }), staffId: 10);

        Assert.Equal(nameof(WorkStatus.Submitted), result.Status);
    }

    [Fact]
    public async Task ApproveWork_ByQc_SetsApproved()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
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

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        var result = await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", new List<string>()), qcId: 3);

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

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        await service.ApproveWorkAsync(work.Id, qcId: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApproveWorkAsync(work.Id, qcId: 3));
    }
}
