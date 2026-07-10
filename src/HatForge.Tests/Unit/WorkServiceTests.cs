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
        decimal estimatedMetersPerUnit = 0m,
        BatchStatus status = BatchStatus.InProduction)
    {
        var batch = new Batch
        {
            BatchNumber = "B-100", HatModelId = 1, TargetQuantity = 100,
            Status = status, AssignedToLeadId = 1,
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
        bool withTransfer = false,
        int? receivedUsableQuantity = null)
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
                ApprovedAt = DateTime.UtcNow,
                ReceivedUsableQuantity = receivedUsableQuantity,
                ReceivedDefectiveQuantity = receivedUsableQuantity.HasValue ? 100 - receivedUsableQuantity.Value : null
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

        var result = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);

        Assert.Equal(nameof(WorkStatus.Submitted), result.Status);
        Assert.Equal(10, result.Quantity);
    }

    [Fact]
    public async Task SubmitWork_CancelledBatch_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx, status: BatchStatus.Cancelled);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(
                new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }),
                staffId: 2));
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
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 1));
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
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2));
    }

    [Fact]
    public async Task SubmitWork_WhenWorkshopCompleted_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        bw.IsCompleted = true;
        await ctx.SaveChangesAsync();
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2));
    }

    [Fact]
    public async Task SubmitWork_WhenBatchPendingLeadReview_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var batch = ctx.Batches.Single(x => x.Id == batchId);
        batch.Status = BatchStatus.PendingLeadReview;
        await ctx.SaveChangesAsync();
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2));
    }

    [Fact]
    public async Task SubmitWork_WithMaterials_RequiresReportedMaterialUsage()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2.5m);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(
                new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }),
                staffId: 2));
    }

    [Fact]
    public async Task SubmitWork_WithoutMaterials_WithReportedUsage_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(
                new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 1m),
                staffId: 2));
    }

    [Fact]
    public async Task SubmitWork_WithMaterials_ReservesReportedUsageAndKeepsEstimate()
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
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 20m), staffId: 2);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(20m, bw.MaterialUsed);
        Assert.Equal(20m, result.ReportedMaterialUsed);
        Assert.Equal(25m, result.EstimatedMaterialUsed);
    }

    [Fact]
    public async Task SubmitWork_WithInsufficientReportedMaterials_Throws()
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
            service.SubmitWorkAsync(
                new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 21m),
                staffId: 2));
    }

    [Fact]
    public async Task RejectWork_WithMaterials_ReconcilesReportedToHigherActualUsage()
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
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 15m), staffId: 2);
        await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", 0, 10, 0, 20m, new List<string>()), qcId: 3);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(20m, bw.MaterialUsed);
    }

    [Fact]
    public async Task RejectWork_WithMaterials_ReconcilesReportedToLowerActualUsage()
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
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 18m), staffId: 2);
        var result = await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", 0, 10, 0, 12m, new List<string>()), qcId: 3);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(12m, bw.MaterialUsed);
        Assert.Equal(12m, result.ActualMaterialUsed);
        Assert.Equal(18m, result.ReportedMaterialUsed);
        Assert.Equal(10, result.RepairableQuantity);
    }

    [Fact]
    public async Task RejectWork_WithMixedRepairableAndUnrepairable_AllowsOnlyRepairableResubmission()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 500, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        var rejected = await service.RejectWorkAsync(
            new RejectWorkDto(work.Id, "250 cần sửa, 250 hỏng không sửa được", 0, 250, 250, 0m, new List<string>()),
            qcId: 3);

        Assert.Equal(250, rejected.RepairableQuantity);
        Assert.Equal(250, rejected.UnrepairableQuantity);

        var newProduction = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 251, false, new List<string> { "/uploads/new-production.jpg" }), staffId: 2);
        Assert.False(newProduction.IsRework);
        await service.ApproveWorkAsync(new ApproveWorkDto(newProduction.Id, 0m, null), qcId: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 251, true, new List<string> { "/uploads/too-many.jpg" }), staffId: 2));

        var rework = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 250, true, new List<string> { "/uploads/rework.jpg" }), staffId: 2);
        Assert.True(rework.IsRework);
        Assert.Equal(250, rework.Quantity);
    }

    [Fact]
    public async Task RejectWork_WhenQcQuantitiesDoNotMatchSubmittedQuantity_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 500, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RejectWorkAsync(
                new RejectWorkDto(work.Id, "Sai tổng số lượng", 250, 100, 100, 0m, new List<string>()),
                qcId: 3));
    }

    [Fact]
    public async Task ApproveWork_WithMaterials_ReconcilesReportedToActualUsage()
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
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 10m), staffId: 2);
        await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 15m, null), qcId: 3);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.Equal(15m, bw.MaterialUsed);
    }

    [Fact]
    public async Task ApproveWork_WithMaterials_ActualUsageExceedsAvailableAfterReportedReserve_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2m);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 80m),
            staffId: 2);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 101m, null), qcId: 3));
    }

    [Fact]
    public async Task RejectWork_WithMaterials_ActualUsageExceedsAvailableAfterReportedReserve_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 2m);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 80m),
            staffId: 2);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RejectWorkAsync(new RejectWorkDto(work.Id, "Overuse", 0, 10, 0, 101m, new List<string>()), qcId: 3));
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
            new SubmitWorkDto(batchId, 1, 7, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 7m), staffId: 2);
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
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 10m), staffId: 2);
        await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Sai màu vải", 0, 0, 10, 10m, new List<string>()), qcId: 3);

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
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 2, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 10));
    }

    [Fact]
    public async Task SubmitWork_SecondWorkshop_WithTransfer_Succeeds()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithTwoWorkshopsAsync(ctx, withTransfer: true, receivedUsableQuantity: 10);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        ctx.Users.Add(new HatForge.Domain.Entities.User
        {
            Id = 10, Email = "staff2@hf.com", Name = "Staff2",
            Role = UserRole.Staff, WorkshopId = 2, PasswordHash = "x"
        });
        await ctx.SaveChangesAsync();

        var result = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 2, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 10);

        Assert.Equal(nameof(WorkStatus.Submitted), result.Status);
    }

    [Fact]
    public async Task SubmitWork_SecondWorkshop_ExceedingReceivedUsableQuantity_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithTwoWorkshopsAsync(ctx, withTransfer: true, receivedUsableQuantity: 9);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        ctx.Users.Add(new HatForge.Domain.Entities.User
        {
            Id = 10, Email = "staff2@hf.com", Name = "Staff2",
            Role = UserRole.Staff, WorkshopId = 2, PasswordHash = "x"
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 2, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 10));
    }

    [Fact]
    public async Task SubmitWork_SecondWorkshop_UsesCumulativeReceivedUsableQuantity()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithTwoWorkshopsAsync(ctx, withTransfer: true, receivedUsableQuantity: 15);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        ctx.Users.Add(new HatForge.Domain.Entities.User
        {
            Id = 10, Email = "staff2@hf.com", Name = "Staff2",
            Role = UserRole.Staff, WorkshopId = 2, PasswordHash = "x"
        });
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 11, workshopId: 2));
        await ctx.SaveChangesAsync();

        var first = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 2, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 10);
        await service.ApproveWorkAsync(new ApproveWorkDto(first.Id, 0m, null), qcId: 11);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitWorkAsync(new SubmitWorkDto(batchId, 2, 6, false, new List<string> { "/uploads/p2.jpg" }), staffId: 10));
    }

    [Fact]
    public async Task SubmitWork_Rework_DoesNotConsumeAdditionalReceivedUsableInput()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithTwoWorkshopsAsync(ctx, withTransfer: true, receivedUsableQuantity: 10);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        ctx.Users.Add(new HatForge.Domain.Entities.User
        {
            Id = 10, Email = "staff2@hf.com", Name = "Staff2",
            Role = UserRole.Staff, WorkshopId = 2, PasswordHash = "x"
        });
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 11, workshopId: 2));
        await ctx.SaveChangesAsync();

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 2, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 10);
        await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Need repair", 0, 10, 0, 0m, new List<string>()), qcId: 11);

        var rework = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 2, 10, true, new List<string> { "/uploads/rework.jpg" }), staffId: 10);

        Assert.True(rework.IsRework);
        Assert.Equal(10, rework.Quantity);
    }

    [Fact]
    public async Task SubmitWork_Rework_WithMaterials_RequiresAndReservesReportedUsage()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            requiresMaterials: true,
            materialsReceived: true,
            initialMaterialQty: 100m,
            estimatedMetersPerUnit: 1m);
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }, ReportedMaterialUsed: 10m),
            staffId: 2);
        await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Need repair", 0, 10, 0, 10m, new List<string>()), qcId: 3);

        var rework = await service.SubmitWorkAsync(
            new SubmitWorkDto(batchId, 1, 10, true, new List<string> { "/uploads/rework.jpg" }, ReportedMaterialUsed: 5m),
            staffId: 2);

        var bw = ctx.BatchWorkshops.Single(x => x.BatchId == batchId && x.WorkshopId == 1);
        Assert.True(rework.IsRework);
        Assert.Equal(5m, rework.ReportedMaterialUsed);
        Assert.Equal(15m, bw.MaterialUsed);
    }

    [Fact]
    public async Task ApproveWork_ByQc_SetsApproved()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        var result = await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 3);

        Assert.Equal(nameof(WorkStatus.Approved), result.Status);
        Assert.Equal(3, result.ReviewedByQCId);
    }

    [Fact]
    public async Task ApproveWork_ByQcOfDifferentWorkshop_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 11, workshopId: 2));
        await ctx.SaveChangesAsync();
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 11));
    }

    [Fact]
    public async Task RejectWork_ByQc_SetsRejectedWithReason()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        var result = await service.RejectWorkAsync(new RejectWorkDto(work.Id, "Loose stitching", 0, 10, 0, 0m, new List<string>()), qcId: 3);

        Assert.Equal(nameof(WorkStatus.Rejected), result.Status);
        Assert.Equal("Loose stitching", result.RejectionNotes);
    }

    [Fact]
    public async Task RejectWork_ByQcOfDifferentWorkshop_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 11, workshopId: 2));
        await ctx.SaveChangesAsync();
        var service = new WorkService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RejectWorkAsync(new RejectWorkDto(work.Id, "Wrong workshop", 0, 10, 0, 0m, new List<string>()), qcId: 11));
    }

    [Fact]
    public async Task ApproveWork_AlreadyApproved_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new WorkService(uow, new NoOpNotificationPublisher());

        var work = await service.SubmitWorkAsync(new SubmitWorkDto(batchId, 1, 10, false, new List<string> { "/uploads/p.jpg" }), staffId: 2);
        await service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 3);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApproveWorkAsync(new ApproveWorkDto(work.Id, 0m, null), qcId: 3));
    }
}
