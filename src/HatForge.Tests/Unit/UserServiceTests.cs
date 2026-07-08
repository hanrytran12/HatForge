using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class UserServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidStaff_CreatesActiveUser()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.CreateAsync(new RegisterDto(
            "NewStaff@HF.com",
            "Staff123!",
            "New Staff",
            UserRole.Staff,
            1));

        Assert.Equal("newstaff@hf.com", result.Email);
        Assert.Equal(nameof(UserRole.Staff), result.Role);
        Assert.Equal("Workshop 1", result.WorkshopName);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveUsers()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(new User
        {
            Email = "inactive@hf.com",
            Name = "Inactive",
            Role = UserRole.Staff,
            WorkshopId = 1,
            PasswordHash = "x",
            IsActive = false
        });
        await ctx.SaveChangesAsync();
        var service = CreateService(ctx);

        var result = await service.GetAllAsync();

        Assert.DoesNotContain(result, x => x.Email == "inactive@hf.com");
        Assert.All(result, x => Assert.True(x.IsActive));
    }

    [Fact]
    public async Task DeleteStaffAsync_WhenWorkshopHasNoWork_DeactivatesStaff()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = CreateService(ctx);

        await service.DeleteStaffAsync(2);

        Assert.False(ctx.Users.Single(x => x.Id == 2).IsActive);
    }

    [Fact]
    public async Task DeleteStaffAsync_WhenWorkshopHasActiveWork_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: 1, orderIndex: 0);
        ctx.Works.Add(new Work
        {
            BatchId = batchId,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 10,
            Status = WorkStatus.Submitted
        });
        await ctx.SaveChangesAsync();
        var service = CreateService(ctx);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteStaffAsync(2));
    }

    [Fact]
    public async Task DeleteStaffAsync_WhenWorkshopTurnHasNotStarted_DeactivatesStaff()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.Staff(id: 8, workshopId: 2));
        await ctx.SaveChangesAsync();

        var batchId = await SeedBatchWithWorkshopAsync(ctx, workshopId: 1, orderIndex: 0);
        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batchId,
            WorkshopId = 2,
            OrderIndex = 1,
            RequiresMaterials = false,
            MaterialsReceived = false,
            IsCompleted = false,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(3)
        });
        await ctx.SaveChangesAsync();
        var service = CreateService(ctx);

        await service.DeleteStaffAsync(8);

        Assert.False(ctx.Users.Single(x => x.Id == 8).IsActive);
    }

    [Fact]
    public async Task DeleteStaffAsync_ForNonStaff_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var service = CreateService(ctx);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteStaffAsync(1));
    }

    private static UserService CreateService(HatForge.Infrastructure.Data.AppDbContext ctx)
        => new(TestDataFactory.CreateUnitOfWork(ctx), new TestPasswordHasher());

    private static async Task<int> SeedBatchWithWorkshopAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        int workshopId,
        int orderIndex)
    {
        var batch = new Batch
        {
            BatchNumber = $"BATCH-{Guid.NewGuid():N}",
            HatModelId = 1,
            TargetQuantity = 100,
            Status = BatchStatus.InProduction,
            AssignedToLeadId = 1,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(7)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id,
            WorkshopId = workshopId,
            OrderIndex = orderIndex,
            RequiresMaterials = false,
            MaterialsReceived = false,
            IsCompleted = false,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(3)
        });
        await ctx.SaveChangesAsync();

        return batch.Id;
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string hash) => hash == Hash(password);
    }
}
