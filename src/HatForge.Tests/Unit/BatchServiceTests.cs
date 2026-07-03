using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class BatchServiceTests
{
    private static readonly DateTime Start = DateTime.UtcNow.Date.AddDays(1);
    private static readonly DateTime End = DateTime.UtcNow.Date.AddDays(31);

    // Helper: workshop item without materials
    private static WorkshopPlanItemDto NoMaterial(int workshopId, int order, DateTime start, DateTime end)
        => new(workshopId, order, false, start, end, null, null);

    // Helper: workshop item with materials
    private static WorkshopPlanItemDto WithMaterial(int workshopId, int order, DateTime start, DateTime end, DateTime deliveryDate)
        => new(workshopId, order, true, start, end, deliveryDate, new List<MaterialItemDto>
        {
            new("Wool Felt", 500),
            new("Thread", 100)
        }, EstimatedMetersPerUnit: 2m);

    [Fact]
    public async Task CreateBatch_WithValidData_CreatesBatch()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var result = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));

        Assert.Matches(@"^BATCH-\d{8}-\d{4}$", result.BatchNumber);
        Assert.Equal(nameof(BatchStatus.Assigned), result.Status);
        Assert.Empty(result.Workshops);
    }

    [Fact]
    public async Task CreateBatch_TwoBatchesSameDay_HaveSequentialNumbers()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var first = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));
        var second = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));

        Assert.NotEqual(first.BatchNumber, second.BatchNumber);
        Assert.EndsWith("-0001", first.BatchNumber);
        Assert.EndsWith("-0002", second.BatchNumber);
    }

    [Fact]
    public async Task CreateBatch_EndBeforeStart_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateBatchAsync(new CreateBatchDto(1, 100, End, Start, 1)));
    }

    [Fact]
    public async Task PlanBatch_ByAssignedLead_CreatesWorkshopsAndDeliveries()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var batch = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));

        var plan = new PlanBatchDto(new List<WorkshopPlanItemDto>
        {
            NoMaterial(1, 0, Start, Start.AddDays(10)),
            WithMaterial(3, 1, Start.AddDays(11), Start.AddDays(20), Start.AddDays(10))
        });

        var result = await service.PlanBatchAsync(batch.Id, plan, leadId: 1);

        Assert.Equal(nameof(BatchStatus.InProduction), result.Status);
        Assert.Equal(2, result.Workshops.Count);
        Assert.False(result.Workshops[0].RequiresMaterials);
        Assert.True(result.Workshops[1].RequiresMaterials);
    }

    [Fact]
    public async Task PlanBatch_ByWrongLead_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var batch = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));
        var plan = new PlanBatchDto(new List<WorkshopPlanItemDto>
        {
            NoMaterial(1, 0, Start, End)
        });

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.PlanBatchAsync(batch.Id, plan, leadId: 999));
    }

    [Fact]
    public async Task PlanBatch_RequiresMaterials_WithoutDeliveryDate_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var batch = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));
        var plan = new PlanBatchDto(new List<WorkshopPlanItemDto>
        {
            new(3, 0, true, Start, End, null, null) // RequiresMaterials but missing all material info
        });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.PlanBatchAsync(batch.Id, plan, leadId: 1));
    }

    [Fact]
    public async Task PlanBatch_WhenMaterialRequirementDoesNotMatchWorkshop_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var batch = await service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 1));
        var plan = new PlanBatchDto(new List<WorkshopPlanItemDto>
        {
            NoMaterial(3, 0, Start, End)
        });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.PlanBatchAsync(batch.Id, plan, leadId: 1));
    }

    [Fact]
    public async Task CreateBatch_WithInvalidLead_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CreateBatchAsync(new CreateBatchDto(1, 100, Start, End, 999)));
    }

    [Fact]
    public async Task MarkWorkshopCompleted_ByAssignedLead_WithApprovedWork_Succeeds()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAndWorkAsync(ctx, WorkStatus.Approved, passedQuantity: 10);
        var service = new BatchService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        var result = await service.MarkWorkshopCompletedAsync(batchId, workshopId: 1, actorId: 1);

        Assert.True(result.Workshops.Single().IsCompleted);
        Assert.Equal(nameof(BatchStatus.PendingLeadReview), result.Status);
    }

    [Fact]
    public async Task MarkWorkshopCompleted_ByWrongLead_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.Lead(id: 9));
        await ctx.SaveChangesAsync();
        var batchId = await SeedBatchWithWorkshopAndWorkAsync(ctx, WorkStatus.Approved, passedQuantity: 10);
        var service = new BatchService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.MarkWorkshopCompletedAsync(batchId, workshopId: 1, actorId: 9));
    }

    [Fact]
    public async Task MarkWorkshopCompleted_WithPendingWork_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAndWorkAsync(ctx, WorkStatus.Submitted, passedQuantity: 0);
        var service = new BatchService(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.MarkWorkshopCompletedAsync(batchId, workshopId: 1, actorId: 1));
    }

    private static async Task<int> SeedBatchWithWorkshopAndWorkAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        WorkStatus workStatus,
        int passedQuantity)
    {
        var batch = new Batch
        {
            BatchNumber = "B-COMPLETE-001",
            HatModelId = 1,
            TargetQuantity = 100,
            Status = BatchStatus.InProduction,
            AssignedToLeadId = 1,
            StartDate = Start,
            EndDate = End
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id,
            WorkshopId = 1,
            OrderIndex = 0,
            RequiresMaterials = false,
            MaterialsReceived = false,
            IsCompleted = false,
            StartDate = Start,
            EndDate = Start.AddDays(10)
        });
        ctx.Works.Add(new Work
        {
            BatchId = batch.Id,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 10,
            Status = workStatus,
            PassedQuantity = passedQuantity
        });
        await ctx.SaveChangesAsync();
        return batch.Id;
    }
}
