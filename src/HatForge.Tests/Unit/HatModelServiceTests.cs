using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class HatModelServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidData_CreatesHatModelCode()
    {
        using var ctx = TestDataFactory.CreateContext();
        var service = new HatModelService(TestDataFactory.CreateUnitOfWork(ctx));

        var result = await service.CreateAsync(new CreateHatModelDto("Baseball Cap", "Cotton cap"));

        Assert.Matches(@"^HAT-\d{8}-\d{4}$", result.Code);
        Assert.Equal("Baseball Cap", result.Name);
        Assert.Single(ctx.HatModels);
    }

    [Fact]
    public async Task CreateAsync_TwoHatModelsSameDay_HaveSequentialCodes()
    {
        using var ctx = TestDataFactory.CreateContext();
        var service = new HatModelService(TestDataFactory.CreateUnitOfWork(ctx));

        var first = await service.CreateAsync(new CreateHatModelDto("Fedora", null));
        var second = await service.CreateAsync(new CreateHatModelDto("Cap", null));

        Assert.NotEqual(first.Code, second.Code);
        Assert.EndsWith("-0001", first.Code);
        Assert.EndsWith("-0002", second.Code);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsHatModelsOrderedByCode()
    {
        using var ctx = TestDataFactory.CreateContext();
        var service = new HatModelService(TestDataFactory.CreateUnitOfWork(ctx));
        ctx.HatModels.Add(new HatModel { Code = "Z-HAT", Name = "Z Hat" });
        ctx.HatModels.Add(new HatModel { Code = "A-HAT", Name = "A Hat" });
        await ctx.SaveChangesAsync();

        var result = await service.GetAllAsync();

        Assert.Equal(new[] { "A-HAT", "Z-HAT" }, result.Select(x => x.Code));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndDescription_ButKeepsCode()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = new HatModelService(TestDataFactory.CreateUnitOfWork(ctx));

        var result = await service.UpdateAsync(1, new UpdateHatModelDto("Updated Fedora", "Updated description"));

        Assert.Equal("FEDORA", result.Code);
        Assert.Equal("Updated Fedora", result.Name);
        Assert.Equal("Updated description", result.Description);
    }

    [Fact]
    public async Task DeleteAsync_WithoutLinkedBatch_RemovesHatModel()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = new HatModelService(TestDataFactory.CreateUnitOfWork(ctx));

        await service.DeleteAsync(1);

        Assert.Empty(ctx.HatModels);
    }

    [Fact]
    public async Task DeleteAsync_WithLinkedBatch_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Batches.Add(new Batch
        {
            BatchNumber = "BATCH-TEST-001",
            HatModelId = 1,
            TargetQuantity = 100,
            Status = BatchStatus.Assigned,
            StartDate = DateTime.UtcNow.Date.AddDays(1),
            EndDate = DateTime.UtcNow.Date.AddDays(2)
        });
        await ctx.SaveChangesAsync();
        var service = new HatModelService(TestDataFactory.CreateUnitOfWork(ctx));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(1));
    }
}
