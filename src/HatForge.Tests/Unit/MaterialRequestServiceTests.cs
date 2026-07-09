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
    private const int AdminId = 4;
    private const int QcTransportId = 7;
    private const int Workshop1Id = 1;
    private const int Workshop2Id = 2;
    private const int Workshop3Id = 3;

    public static IEnumerable<object[]> SeedBaseData() => new List<object[]>
    {
        new object[] { 1, 2, 3, 1 }
    };

    private static MaterialRequestService CreateService(AppDbContext ctx)
        => new(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

    private static LeadTaskDelegationService CreateDelegationService(AppDbContext ctx)
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
        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id,
            WorkshopId = Workshop1Id,
            OrderIndex = 0,
            RequiresMaterials = true,
            MaterialsReceived = false,
            IsCompleted = false,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10)
        });
        await ctx.SaveChangesAsync();

        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id, MaterialName = "Wool Felt",
            Unit = "m",
            PlannedQuantity = planned1
        });
        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id, MaterialName = "Thread",
            Unit = "m",
            PlannedQuantity = planned2
        });
        await ctx.SaveChangesAsync();

        await TestDataFactory.SeedLeadStockAsync(ctx, LeadId, "Wool Felt", "m", planned1 + 1000);
        await TestDataFactory.SeedLeadStockAsync(ctx, LeadId, "Thread", "m", planned2 + 1000);

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
    public async Task ConfirmDelivery_MissingDeliveryItem_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (_, deliveryId, item1, _) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        var service = CreateDeliveryService(ctx);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDeliveryAsync(new ConfirmMaterialDeliveryDto(
                deliveryId,
                new List<ConfirmMaterialItemDto> { new(item1, 400) }), QcId));
    }

    [Fact]
    public async Task ConfirmDelivery_ZeroActualQuantity_CreatesShortfallRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        var service = CreateDeliveryService(ctx);

        await service.ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 0, item2, 100), QcId);

        var request = Assert.Single(await CreateService(ctx).GetByBatchAsync(batchId));
        Assert.Equal(500, Assert.Single(request.Items).ShortfallQuantity);
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
    public async Task ConfirmDelivery_ShortfallForNonFirstWorkshop_DoesNotCreateMaterialRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));

        var batch = new Batch
        {
            BatchNumber = "B-MR-NONFIRST", HatModelId = 1,
            TargetQuantity = 100, Status = BatchStatus.InProduction,
            AssignedToLeadId = LeadId,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id, WorkshopId = Workshop3Id,
            OrderIndex = 1, RequiresMaterials = true,
            MaterialsReceived = false,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10)
        });
        var delivery = new MaterialDelivery
        {
            BatchId = batch.Id,
            WorkshopId = Workshop3Id,
            ScheduledDate = DateTime.UtcNow,
            Status = MaterialDeliveryStatus.Scheduled
        };
        ctx.MaterialDeliveries.Add(delivery);
        await ctx.SaveChangesAsync();

        var item = new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id,
            MaterialName = "Wool Felt",
            PlannedQuantity = 100
        };
        ctx.MaterialDeliveryItems.Add(item);
        await ctx.SaveChangesAsync();

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            new ConfirmMaterialDeliveryDto(delivery.Id, new List<ConfirmMaterialItemDto>
            {
                new(item.Id, 50)
            }), qcId: 7);

        var requests = await CreateService(ctx).GetByBatchAsync(batch.Id);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task ConfirmDelivery_Oversupply_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        var service = CreateDeliveryService(ctx);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDeliveryAsync(
                BuildConfirm(deliveryId, item1, 600, item2, 150), QcId));
    }

    [Fact]
    public async Task ConfirmDelivery_WhenTransportDelegationActive_WaitsForTransportDelivery()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcTransport(QcTransportId));
        await ctx.SaveChangesAsync();
        var (_, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx);

        var delegationService = CreateDelegationService(ctx);
        var created = await delegationService.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.MaterialDelivery,
                deliveryId,
                QcTransportId,
                null),
            LeadId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateDeliveryService(ctx).ConfirmDeliveryAsync(
                BuildConfirm(deliveryId, item1, 500, item2, 100),
                QcId));

        await delegationService.ApproveAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto(null));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateDeliveryService(ctx).ConfirmDeliveryAsync(
                BuildConfirm(deliveryId, item1, 500, item2, 100),
                QcId));

        await delegationService.MarkMaterialDeliveredAsync(created.Id, QcTransportId);

        var result = await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 500, item2, 100),
            QcId);

        Assert.True(result.IsConfirmed);
        Assert.Equal(nameof(MaterialDeliveryStatus.Received), result.Status);
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

        var woolStock = ctx.LeadMaterialStocks.Single(x => x.MaterialName == "Wool Felt");
        Assert.Equal(1400m, woolStock.QuantityOnHand);
        var tx = Assert.Single(ctx.LeadMaterialStockTransactions);
        Assert.Equal(LeadMaterialStockTransactionType.MaterialRequestAllocation, tx.Type);
        Assert.Equal(-100m, tx.QuantityDelta);
        Assert.Equal(approved.Id, tx.MaterialRequestId);
    }

    [Fact]
    public async Task ApproveAsync_WhenLeadInventoryInsufficient_ThrowsAndLeavesRequestPending()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx);

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var stock = ctx.LeadMaterialStocks.Single(x => x.MaterialName == "Wool Felt");
        stock.QuantityOnHand = 50m;
        ctx.LeadMaterialStocks.Update(stock);
        await ctx.SaveChangesAsync();

        var mrService = CreateService(ctx);
        var pending = (await mrService.GetByBatchAsync(batchId)).Single();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            mrService.ApproveAsync(pending.Id, LeadId));

        var reloaded = (await mrService.GetByBatchAsync(batchId)).Single();
        Assert.Equal(nameof(MaterialRequestStatus.Pending), reloaded.Status);
        Assert.Equal(50m, ctx.LeadMaterialStocks.Single(x => x.MaterialName == "Wool Felt").QuantityOnHand);
        Assert.Empty(ctx.LeadMaterialStockTransactions);
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
    public async Task ConfirmAsync_WhenTransportDelegationActive_WaitsForTransportDelivery()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcTransport(QcTransportId));
        await ctx.SaveChangesAsync();
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);
        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var mrService = CreateService(ctx);
        var pending = (await mrService.GetByBatchAsync(batchId)).Single();
        var approved = await mrService.ApproveAsync(pending.Id, LeadId);
        var delegationService = CreateDelegationService(ctx);
        var delegation = await delegationService.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.MaterialRequestFulfillment,
                approved.Id,
                QcTransportId,
                null),
            LeadId);

        var confirmDto = new ConfirmMaterialRequestDto(
            approved.Id,
            new List<ConfirmMaterialRequestItemDto>
            {
                new(approved.Items[0].Id, 100)
            });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).ConfirmAsync(confirmDto, QcId));

        await delegationService.ApproveAsync(delegation.Id, AdminId, new ReviewLeadTaskDelegationDto(null));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).ConfirmAsync(confirmDto, QcId));

        await delegationService.MarkMaterialRequestDeliveredAsync(delegation.Id, QcTransportId);

        var delivered = (await CreateService(ctx).GetByBatchAsync(batchId)).Single(x => x.Id == approved.Id);
        Assert.Equal(QcTransportId, delivered.DeliveredByTransportQcId);
        Assert.Equal("QC Transport", delivered.DeliveredByTransportQcName);
        Assert.NotNull(delivered.DeliveredAt);

        var result = await CreateService(ctx).ConfirmAsync(confirmDto, QcId);

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
    public async Task ConfirmAsync_MissingRequestItem_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var (batchId, deliveryId, item1, item2) = await SeedShortDeliveryAsync(ctx, planned1: 500, planned2: 100);

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 90), QcId);

        var pending = (await CreateService(ctx).GetByBatchAsync(batchId)).Single();
        Assert.Equal(2, pending.Items.Count);
        var approved = await CreateService(ctx).ApproveAsync(pending.Id, LeadId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).ConfirmAsync(
                new ConfirmMaterialRequestDto(approved.Id, new List<ConfirmMaterialRequestItemDto>
                {
                    new(approved.Items[0].Id, 100)
                }), QcId));
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

        // Round 3: approve, confirm short → round 4 (the third supplemental round)
        var a3 = await mrService.ApproveAsync(r3.Id, LeadId);
        var r4 = await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(a3.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(a3.Items[0].Id, 10)
            }), QcId);
        Assert.Equal(4, r4.Round);

        // Round 4: still short → must throw because no more supplemental rounds remain
        var a4 = await mrService.ApproveAsync(r4.Id, LeadId);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            mrService.ConfirmAsync(
                new ConfirmMaterialRequestDto(a4.Id, new List<ConfirmMaterialRequestItemDto>
                {
                    new(a4.Items[0].Id, 1)
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
        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batchB.Id,
            WorkshopId = Workshop1Id,
            OrderIndex = 0,
            RequiresMaterials = true,
            MaterialsReceived = false,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10)
        });
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

        await CreateDeliveryService(ctx).ConfirmDeliveryAsync(
            BuildConfirm(deliveryId, item1, 400, item2, 100), QcId);

        var reloaded = await TestDataFactory.CreateUnitOfWork(ctx)
            .BatchWorkshops.FirstOrDefaultAsync(x => x.BatchId == batchId && x.WorkshopId == Workshop1Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.MaterialsReceived);
    }

    // ─── Ad-hoc material request (QC-initiated top-up, anytime in production) ───

    private static async Task<int> SeedBatchWithWorkshopAsync(
        AppDbContext ctx,
        BatchStatus status = BatchStatus.InProduction,
        int workshopId = Workshop1Id,
        int leadId = LeadId,
        bool requiresMaterials = true,
        int orderIndex = 0)
    {
        var batch = new Batch
        {
            BatchNumber = $"B-AH-{Guid.NewGuid():N}".Substring(0, 12),
            HatModelId = 1, TargetQuantity = 100, Status = status,
            AssignedToLeadId = leadId,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id, WorkshopId = workshopId, OrderIndex = orderIndex,
            RequiresMaterials = requiresMaterials, MaterialsReceived = true, IsCompleted = false,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10)
        });
        await ctx.SaveChangesAsync();
        await TestDataFactory.SeedLeadStockAsync(ctx, leadId, "Wool Felt", "m", 1000);
        return batch.Id;
    }
    private static CreateAdHocMaterialRequestDto BuildAdHoc(
        int batchId, int workshopId = Workshop3Id)
        => new(batchId, workshopId, "Hao hụt khi cắt vải",
            new List<CreateAdHocMaterialRequestItemDto>
            {
                new("Wool Felt", "m", 50)
            });

    [Fact]
    public async Task CreateAdHoc_ByWorkshopQC_Success()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        // Add QC for workshop 3 (the materials-requiring workshop)
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: Workshop3Id);

        var result = await CreateService(ctx).CreateAdHocRequestAsync(
            BuildAdHoc(batchId, Workshop3Id), qcId: 7);

        Assert.Equal(nameof(MaterialRequestStatus.Pending), result.Status);
        Assert.True(result.IsAdHoc);
        Assert.Equal("Hao hụt khi cắt vải", result.Reason);
        Assert.Equal(1, result.Round);
        Assert.Equal(0, result.OriginalDeliveryId);
        Assert.Equal(Workshop3Id, result.WorkshopId);
        Assert.Single(result.Items);
        Assert.Equal("Wool Felt", result.Items[0].MaterialName);
        Assert.Equal(50, result.Items[0].ShortfallQuantity);
    }

    [Fact]
    public async Task CreateAdHoc_ForNonFirstWorkshop_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(
            ctx,
            workshopId: Workshop3Id,
            requiresMaterials: true,
            orderIndex: 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7));
    }

    [Fact]
    public async Task CreateAdHoc_ByWrongWorkshop_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: QcIdWorkshop2, workshopId: Workshop2Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(ctx);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService(ctx).CreateAdHocRequestAsync(
                BuildAdHoc(batchId, Workshop1Id), QcIdWorkshop2));
    }

    [Fact]
    public async Task CreateAdHoc_ByStaff_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService(ctx).CreateAdHocRequestAsync(BuildAdHoc(batchId), StaffId));
    }

    [Fact]
    public async Task CreateAdHoc_OnCompletedBatch_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(ctx, status: BatchStatus.Completed, workshopId: Workshop3Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7));
    }

    [Fact]
    public async Task CreateAdHoc_WorkshopDoesNotRequireMaterials_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        // Workshop 1 is seeded with RequiresMaterials=false; QC of workshop 1 attempts to create a request
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: Workshop1Id, requiresMaterials: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop1Id), QcId));
    }

    [Fact]
    public async Task CreateAdHoc_WorkshopNotInChain_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: QcIdWorkshop2, workshopId: Workshop2Id));
        await ctx.SaveChangesAsync();
        // Batch only has Workshop1 in its chain; QC of Workshop2 requests for Workshop2.
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: Workshop1Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(ctx).CreateAdHocRequestAsync(
                BuildAdHoc(batchId, Workshop2Id), QcIdWorkshop2));
    }

    [Fact]
    public async Task CreateAdHoc_WhenOpenAdHocExists_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: Workshop3Id);

        var mrService = CreateService(ctx);
        var first = await mrService.CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7);

        // Same QC tries to create another ad-hoc while the first is still Pending
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            mrService.CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7));

        // Lead approves it — now it's Approved, still should block new requests
        await mrService.ApproveAsync(first.Id, LeadId);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            mrService.CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7));
    }

    [Fact]
    public async Task CreateAdHoc_AfterFulfilled_AllowsNewRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: Workshop3Id);

        var mrService = CreateService(ctx);
        var first = await mrService.CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7);
        var approved = await mrService.ApproveAsync(first.Id, LeadId);
        await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(approved.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(approved.Items[0].Id, 50)
            }), qcId: 7);

        // Now Fulfilled — a new ad-hoc request is allowed
        var second = await mrService.CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7);
        Assert.Equal(MaterialRequestStatus.Pending.ToString(), second.Status);
    }

    [Fact]
    public async Task CreateAdHoc_ThenApproveAndConfirm_CompletesFlow()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 7, workshopId: Workshop3Id));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: Workshop3Id);

        var mrService = CreateService(ctx);
        var created = await mrService.CreateAdHocRequestAsync(BuildAdHoc(batchId, Workshop3Id), qcId: 7);

        var approved = await mrService.ApproveAsync(created.Id, LeadId);
        Assert.Equal(nameof(MaterialRequestStatus.Approved), approved.Status);

        var fulfilled = await mrService.ConfirmAsync(
            new ConfirmMaterialRequestDto(approved.Id, new List<ConfirmMaterialRequestItemDto>
            {
                new(approved.Items[0].Id, 50)
            }), qcId: 7);

        Assert.Equal(nameof(MaterialRequestStatus.Fulfilled), fulfilled.Status);
        Assert.True(fulfilled.IsAdHoc);

        var all = await mrService.GetByBatchAsync(batchId);
        Assert.Single(all);
    }
}
