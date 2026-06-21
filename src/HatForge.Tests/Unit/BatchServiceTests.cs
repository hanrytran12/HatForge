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

    [Fact]
    public async Task CreateBatch_WithValidData_CreatesBatch()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var dto = new CreateBatchDto(1, 100, Start, End, 1);
        var result = await service.CreateBatchAsync(dto);

        Assert.Matches(@"^BATCH-\d{8}-\d{4}$", result.BatchNumber);
        Assert.Equal(nameof(BatchStatus.Assigned), result.Status);
        Assert.Empty(result.Workshops); // No workshops yet — Lead must plan
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

        var dto = new CreateBatchDto(1, 100, End, Start, 1); // End < Start

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBatchAsync(dto));
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
            new(1, 0, false, Start, Start.AddDays(10), null),
            new(2, 1, true, Start.AddDays(11), Start.AddDays(20), Start.AddDays(10))
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
            new(1, 0, false, Start, End, null)
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
            new(1, 0, true, Start, End, null) // RequiresMaterials but no delivery date
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

        var dto = new CreateBatchDto(1, 100, Start, End, 999);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBatchAsync(dto));
    }
}
