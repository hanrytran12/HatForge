using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Infrastructure.Data;
using HatForge.Infrastructure.Repositories;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class MaterialRequestServiceTests
{
    private const int LeadId = 1;
    private const int StaffId = 2;
    private const int QcId = 3;
    private const int QcIdWorkshop2 = 5;
    private const int Workshop1Id = 1;
    private const int Workshop2Id = 2;

    private static MaterialRequestService CreateService(AppDbContext ctx)
        => new(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

    private static MaterialDeliveryService CreateDeliveryService(AppDbContext ctx)
    {
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var notif = new NoOpNotificationPublisher();
        var mr = new MaterialRequestService(uow, notif);
        return new MaterialDeliveryService(uow, notif, mr);
    }

    private static async Task<(int batchId, int deliveryId, int itemId1, int itemId2)> SeedShortDeliveryAsync(
        AppDbContext ctx,
        int planned1 = 500,
        int planned2 = 100)
    {
        var batch = new Batch
        {
            BatchNumber = "B-MR-001", HatModelId = 1,
            TargetQuantity = 100, Status = BatchStatus.InProduction,
            AssignedToLeadId = LeadId,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        var delivery = new MaterialDelivery
        {
            BatchId = batch.Id, WorkshopId = Workshop1Id,
            ScheduledDate = DateTime.UtcNow, Status = MaterialDeliveryStatus.Scheduled
        };
        ctx.MaterialDeliveries.Add(delivery);
        await ctx.SaveChangesAsync();

        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id, MaterialName = "Wool Felt",
            PlannedQuantity = planned1
        });
        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id, MaterialName = "Thread",
            PlannedQuantity = planned2
        });
        await ctx.SaveChangesAsync();

        var items = await new UnitOfWork(ctx).MaterialDeliveryItems
            .FindAsync(x => x.MaterialDeliveryId == delivery.Id);
        var item1 = items.First(i => i.MaterialName == "Wool Felt");
        var item2 = items.First(i => i.MaterialName == "Thread");
        return (batch.Id, delivery.Id, item1.Id, item2.Id);
    }

    private static ConfirmMaterialDeliveryDto BuildConfirm(
        int deliveryId, int itemId1, int actual1, int itemId2, int actual2)
        => new(deliveryId, new List<ConfirmMaterialItemDto>
        {
            new(itemId1, actual1),
            new(itemId2, actual2)
        });

    [Fact]
    public async Task ConfirmDelivery_SingleItemShort_CreatesMaterialRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        var service = CreateDeliveryService(ctx);

        // Wool Felt 400/500 (short 100), Thread 100/100 (exact)
        var result = await service.ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        Assert.True(result.IsConfirmed);

        var requests = await new MaterialRequestService(
            TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher()
        ).GetByBatchAsync(batchId);

        Assert.Single(requests);
        Assert.Equal(nameof(MaterialRequestStatus.Pending), requests[0].Status);
        Assert.Single(requests[0].Items);
        Assert.Equal("Wool Felt", requests[0].Items[0].MaterialName);
        Assert.Equal(100, requests[0].Items[0].ShortfallQuantity);
        Assert.Null(requests[0].Items[0].ActualQuantity);
    }

    [Fact]
    public async Task ConfirmDelivery_AllItemsExact_NoMaterialRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx);

        var service = CreateDeliveryService(ctx);
        await service.ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 500, item2, 100), QcId);

        var mrService = CreateService(ctx);
        var requests = await mrService.GetByBatchAsync(batchId);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task ConfirmDelivery_Oversupply_DoesNotCreateRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        var service = CreateDeliveryService(ctx);
        await service.ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 600, item2, 150), QcId);

        var mrService = CreateService(ctx);
        var requests = await mrService.GetByBatchAsync(batchId);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task ApproveAsync_ByAssignedLead_TransitionsToApproved()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx);

        var deliveryService = CreateDeliveryService(ctx);
        await deliveryService.ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var mrService = CreateService(ctx);
        var pending = (await mrService.GetByBatchAsync(batchId)).Single();
        Assert.Equal(nameof(MaterialRequestStatus.Pending), pending.Status);

        var approved = await mrService.ApproveAsync(pending.Id, LeadId);
        Assert.Equal(nameof(MaterialRequestStatus.Approved), approved.Status);
        Assert.Equal(LeadId, approved.ApprovedByLeadId);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task ApproveAsync_ByWrongLead_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx);
        ctx.Users.Add(new User
        {
            Id = 99, Email = "other-lead@hf.com", Name = "Other Lead",
            Role = UserRole.Lead, PasswordHash = "x"
        });
        await ctx.SaveChangesAsync();

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var pending = (await CreateService(ctx).GetByBatchAsync(batchId)).Single();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService(ctx).ApproveAsync(pending.Id, leadId: 99));
    }

    [Fact]
    public async Task ApproveAsync_AlreadyApproved_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx);
        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var pending = (await CreateService(ctx).GetByBatchAsync(batchId)).Single();
        var mrService = CreateService(ctx);
        await mrService.ApproveAsync(pending.Id, LeadId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            mrService.ApproveAsync(pending.Id, LeadId));
    }

    [Fact]
    public async Task ConfirmAsync_ByCorrectWorkshop_AllSatisfied_MarksFulfilled()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);
        // Item2 not short so not in MR; only item1 is.
        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var pending = (await CreateService(ctx).GetByBatchAsync(batchId)).Single();
        var mrService = CreateService(ctx);
        var approved = await mrService.ApproveAsync(pending.Id, LeadId);

        var result = await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(approved.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(approved.Items[0].Id, 100)
            }), QcId);

        Assert.Equal(nameof(MaterialRequestStatus.Fulfilled), result.Status);
    }

    [Fact]
    public async Task ConfirmAsync_StillShort_CreatesNewRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);
        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var mrService = CreateService(ctx);
        var pending = (await mrService.GetByBatchAsync(batchId)).Single();
        var approved = await mrService.ApproveAsync(pending.Id, LeadId);

        // Confirm only 30 of 100 — still short 70
        var next = await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(approved.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(approved.Items[0].Id, 30)
            }), QcId);

        // Returned DTO is the NEW pending request
        Assert.Equal(nameof(MaterialRequestStatus.Pending), next.Status);
        Assert.Equal(2, next.Round);
        Assert.Single(next.Items);
        Assert.Equal(70, next.Items[0].ShortfallQuantity);
        Assert.Null(next.Items[0].ActualQuantity);

        var all = await mrService.GetByBatchAsync(batchId);
        Assert.Equal(2, all.Count);
        Assert.Contains(all, r => r.Status == nameof(MaterialRequestStatus.Fulfilled));
        Assert.Contains(all, r => r.Status == nameof(MaterialRequestStatus.Pending));
    }

    [Fact]
    public async Task ConfirmAsync_ByWrongWorkshop_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: QcIdWorkshop2, workshopId: Workshop2Id));
        await ctx.SaveChangesAsync();

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var pending = (await CreateService(ctx).GetByBatchAsync(batchId)).Single();
        var approved = await CreateService(ctx).ApproveAsync(pending.Id, LeadId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService(ctx).ConfirmAsync(
                new ConfirmMaterialRequestDto(approved.Id, new List<ConfirmMaterialRequestItemDto>
                {
                    new(approved.Items[0].Id, 100)
                }), QcIdWorkshop2));
    }

    [Fact]
    public async Task ConfirmAsync_BeyondMaxRounds_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var mrService = CreateService(ctx);

        // Round 1: created, approve, confirm short → round 2
        var r1 = (await mrService.GetByBatchAsync(batchId)).Single();
        var a1 = await mrService.ApproveAsync(r1.Id, LeadId);
        var r2 = await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(a1.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(a1.Items[0].Id, 50)
            }), QcId);
        Assert.Equal(2, r2.Round);

        // Round 2: approve, confirm short → round 3
        var a2 = await mrService.ApproveAsync(r2.Id, LeadId);
        var r3 = await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(a2.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(a2.Items[0].Id, 20)
            }), QcId);
        Assert.Equal(3, r3.Round);

        // Round 3: approve, confirm short → must throw (4th would be round 4 > max 3)
        var a3 = await mrService.ApproveAsync(r3.Id, LeadId);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            mrService.ConfirmAsync(
                new ConfirmMaterialRequestDto(a3.Id, new List<ConfirmMaterialRequestItemDto>
                {
                    new(a3.Items[0].Id, 10)
                }), QcId));
    }

    [Fact]
    public async Task GetPendingForLeadAsync_FiltersByAssignedLead()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);

        // Batch A assigned to LeadId=1
        var (batchIdA, deliveryIdA, itemA1, itemA2) = await SeedShortDeliveryAsync(ctx);
        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryIdA, itemA1, 400, itemA2, 100), QcId);

        // Batch B assigned to a different lead (id=99)
        var batchB = new Batch
        {
            BatchNumber = "B-MR-002", HatModelId = 1, TargetQuantity = 50,
            Status = BatchStatus.InProduction, AssignedToLeadId = 99,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10)
        };
        ctx.Batches.Add(batchB);
        await ctx.SaveChangesAsync();
        var deliveryB = new MaterialDelivery
        {
            BatchId = batchB.Id, WorkshopId = Workshop1Id,
            ScheduledDate = DateTime.UtcNow, Status = MaterialDeliveryStatus.Scheduled
        };
        ctx.MaterialDeliveries.Add(deliveryB);
        await ctx.SaveChangesAsync();
        var itemB1 = new MaterialDeliveryItem
        {
            MaterialDeliveryId = deliveryB.Id, MaterialName = "Yarn",
            PlannedQuantity = 200
        };
        ctx.MaterialDeliveryItems.Add(itemB1);
        await ctx.SaveChangesAsync();
        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            new ConfirmMaterialDeliveryDto(deliveryB.Id, new List<ConfirmMaterialItemDto>
            {
                new(itemB1.Id, 150)
            }), QcId);

        var pendingForLead1 = await CreateService(ctx).GetPendingForLeadAsync(LeadId);
        var pendingForLead99 = await CreateService(ctx).GetPendingForLeadAsync(99);

        Assert.Single(pendingForLead1);
        Assert.Equal(batchIdA, pendingForLead1[0].BatchId);
        Assert.Single(pendingForLead99);
        Assert.Equal(batchB.Id, pendingForLead99[0].BatchId);
    }

    [Fact]
    public async Task BatchFlow_UnblockedByShortfall()
    {
        // Independent verification: confirm-delivery on a short delivery still marks
        // BatchWorkshop.MaterialsReceived = true, so the batch flow is not blocked.
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        // Create the BatchWorkshop entry that confirms
        var bw = new BatchWorkshop
        {
            BatchId = batchId, WorkshopId = Workshop1Id, OrderIndex = 0,
            RequiresMaterials = true, MaterialsReceived = false, IsCompleted = false,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10)
        };
        ctx.BatchWorkshops.Add(bw);
        await ctx.SaveChangesAsync();

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var reloaded = await TestDataFactory.CreateUnitOfWork(ctx)
            .BatchWorkshops.FirstOrDefaultAsync(x => x.Id == bw.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.MaterialsReceived);
    }
}