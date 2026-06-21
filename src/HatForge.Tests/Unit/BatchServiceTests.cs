using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class BatchServiceTests
{
    [Fact]
    public async Task CreateBatch_WithValidData_CreatesBatchAndWorkshops()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var dto = new CreateBatchDto("B-001", 1, 100, new List<int> { 1, 2 }, 1);
        var result = await service.CreateBatchAsync(dto);

        Assert.Equal("B-001", result.BatchNumber);
        Assert.Equal(nameof(BatchStatus.Assigned), result.Status);
        Assert.Equal(2, result.Workshops.Count);
        Assert.Equal(0, result.Workshops[0].OrderIndex);
    }

    [Fact]
    public async Task CreateBatch_WithDuplicateNumber_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var dto = new CreateBatchDto("B-001", 1, 100, new List<int> { 1 }, 1);
        await service.CreateBatchAsync(dto);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBatchAsync(dto));
    }

    [Fact]
    public async Task CreateBatch_WithNoWorkshops_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var dto = new CreateBatchDto("B-002", 1, 100, new List<int>(), 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateBatchAsync(dto));
    }

    [Fact]
    public async Task CreateBatch_WithInvalidLead_Throws()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var dto = new CreateBatchDto("B-003", 1, 100, new List<int> { 1 }, 999);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateBatchAsync(dto));
    }

    [Fact]
    public async Task MarkWorkshopCompleted_AllComplete_CompletesBatch()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var uow = TestDataFactory.CreateUnitOfWork(ctx);
        var service = new BatchService(uow, new NoOpNotificationPublisher());

        var created = await service.CreateBatchAsync(new CreateBatchDto("B-004", 1, 50, new List<int> { 1 }, 1));
        var result = await service.MarkWorkshopCompletedAsync(created.Id, 1);

        Assert.Equal(nameof(BatchStatus.Completed), result.Status);
        Assert.NotNull(result.CompletedAt);
    }
}
