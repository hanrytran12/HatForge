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
        bool materialsReceived = false,
        decimal initialMaterialQty = 0m,
        decimal materialUsed = 0m,
        decimal estimatedMetersPerUnit = 0m)
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
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10),
            InitialMaterialQty = initialMaterialQty,
            MaterialUsed = materialUsed,
            EstimatedMetersPerUnit = estimatedMetersPerUnit
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
    public async Task SubmitWork_WithMaterials_DeductsEstimatedUsageImmediately()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2.5m);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var result = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(25m, bw.MaterialUsed);
        Assert.Equal(25m, result.EstimatedMaterialUsed);
    }

    [Fact]
    public async Task SubmitWork_WithInsufficientEstimatedMaterials_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 20m,
            estimatedMetersPerUnit: 2.5m);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2));
    }

    [Fact]
    public async Task RejectWork_WithMaterials_DoesNotRefundEstimatedUsage()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2m);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", 20m, new List<string>()), qcId: 3);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(20m, bw.MaterialUsed);
    }

    [Fact]
    public async Task RejectWork_WithMaterials_ReconcilesEstimateToActualUsage()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2m);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        var result = await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", 12m, new List<string>()), qcId: 3);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(12m, bw.MaterialUsed);
        Assert.Equal(12m, result.ActualMaterialUsed);
    }

    [Fact]
    public async Task ApproveWork_WithMaterials_ReconcilesEstimateToActualUsage()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2m);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 15m, null), qcId: 3);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(15m, bw.MaterialUsed);
    }

    [Fact]
    public async Task ApproveWork_WhenMaterialsLow_IncludesDeliveredMaterialDetailsInAlert()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 10m,
            estimatedMetersPerUnit: 1m);

        var delivery = new MaterialDelivery
        {
            BatchId = batchId,
            WorkshopId = 1,
            ScheduledDate = DateTime.UtcNow,
            IsConfirmed = true,
            Status = MaterialDeliveryStatus.Received
        };
        ctx.MaterialDeliveries.Add(delivery);
        await ctx.SaveChangesAsync();

        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id,
            MaterialName = "Vải kaki",
            PlannedQuantity = 10,
            ActualQuantity = 10
        });
        await ctx.SaveChangesAsync();

        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var notifications = new NoOpNotificationPublisher();
        var service = new WorkService(uow, notifications);

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 7, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 7m, null), qcId: 3);

        var payload = Assert.IsType<MaterialLowAlertPayload>(notifications.LastMaterialLowAlertPayload);
        var material = Assert.Single(payload.Materials);
        Assert.Equal("Vải kaki", material.MaterialName);
        Assert.Equal(10, material.ActualQuantity);
        Assert.Equal(3m, payload.MaterialRemaining);
    }

    [Fact]
    public async Task RejectWork_WhenMaterialsOut_SendsMaterialAlert()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 10m,
            estimatedMetersPerUnit: 1m);

        var delivery = new MaterialDelivery
        {
            BatchId = batchId,
            WorkshopId = 1,
            ScheduledDate = DateTime.UtcNow,
            IsConfirmed = true,
            Status = MaterialDeliveryStatus.Received
        };
        ctx.MaterialDeliveries.Add(delivery);
        await ctx.SaveChangesAsync();

        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id,
            MaterialName = "Vải",
            PlannedQuantity = 10,
            ActualQuantity = 10
        });
        await ctx.SaveChangesAsync();

        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var notifications = new NoOpNotificationPublisher();
        var service = new WorkService(uow, notifications);

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Sai màu vải", 10m, new List<string>()), qcId: 3);

        var payload = Assert.IsType<MaterialLowAlertPayload>(notifications.LastMaterialLowAlertPayload);
        Assert.Equal(0m, payload.MaterialRemaining);
        Assert.Equal("Vải", Assert.Single(payload.Materials).MaterialName);
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
        var result = await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 3);

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
        var result = await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", 0m, new List<string>()), qcId: 3);

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
        await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 3));
    }
}
