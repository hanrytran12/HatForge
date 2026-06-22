using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class BatchServiceTests
{
    private static readonly DateTime Start = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

    // Helper: workshop item without materials
    private static WorkshopPlanItemDto NoMaterial(int workshopId, int order, DateTime start, DateTime end)
        => new(workshopId, order, false, start, end, null, null);

    // Helper: workshop item with materials
    private static WorkshopPlanItemDto WithMaterial(int workshopId, int order, DateTime start, DateTime end, DateTime deliveryDate)
        => new(workshopId, order, true, start, end, deliveryDate, new List<MaterialItemDto>
        {
            new("Wool Felt", 500),
            new("Thread", 100)
        });

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
            WithMaterial(2, 1, Start.AddDays(11), Start.AddDays(20), Start.AddDays(10))
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
            new(1, 0, true, Start, End, null, null) // RequiresMaterials but missing all material info
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
}
