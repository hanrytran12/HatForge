using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsProductionDelegationAndUserSummaries()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.QcTransport());
        ctx.Users.Add(TestDataFactory.Staff(id: 8, workshopId: 2));
        ctx.Users.Add(new User
        {
            Id = 9,
            Email = "inactive@hf.com",
            Name = "Inactive Staff",
            Role = UserRole.Staff,
            WorkshopId = 1,
            PasswordHash = "x",
            IsActive = false
        });
        await ctx.SaveChangesAsync();

        var currentBatch = await AddBatchAsync(ctx, "BATCH-CURRENT", BatchStatus.InProduction, DateTime.UtcNow.Date.AddDays(7));
        var overdueBatch = await AddBatchAsync(ctx, "BATCH-OVERDUE", BatchStatus.ReadyForTransfer, DateTime.UtcNow.Date.AddDays(-1));
        await AddBatchAsync(
            ctx,
            "BATCH-COMPLETE",
            BatchStatus.Completed,
            DateTime.UtcNow.Date.AddDays(-2),
            completedAt: DateTime.UtcNow);

        ctx.Works.Add(new Work
        {
            BatchId = currentBatch.Id,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 10,
            Status = WorkStatus.Submitted
        });
        ctx.Works.Add(new Work
        {
            BatchId = overdueBatch.Id,
            WorkshopId = 1,
            StaffId = 2,
            Quantity = 6,
            Status = WorkStatus.Approved
        });

        for (var i = 0; i < 6; i++)
        {
            ctx.LeadTaskDelegationRequests.Add(new LeadTaskDelegationRequest
            {
                BatchId = currentBatch.Id,
                Type = LeadTaskDelegationType.TransferApproval,
                TransferRequestId = 100 + i,
                Status = LeadTaskDelegationStatus.PendingAdminApproval,
                RequestedByLeadId = 1,
                AssignedTransportQcId = 7,
                Reason = $"request-{i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        await ctx.SaveChangesAsync();
        var service = new AdminDashboardService(TestDataFactory.CreateUnitOfWork(ctx));

        var result = await service.GetAsync();

        Assert.Equal(2, result.Kpis.ActiveBatches);
        Assert.Equal(1, result.Kpis.OverdueBatches);
        Assert.Equal(1, result.Kpis.CompletedThisMonth);
        Assert.Equal(6, result.Kpis.PendingAdminDelegations);
        Assert.Equal(6, result.Kpis.ActiveUsers);
        Assert.Equal(2, result.Kpis.ActiveStaff);
        Assert.Equal(1, result.BatchStatusCounts.Single(x => x.Status == nameof(BatchStatus.InProduction)).Count);
        Assert.Equal(5, result.LatestPendingDelegations.Count);
        Assert.Equal("request-5", result.LatestPendingDelegations[0].Reason);
        Assert.DoesNotContain(result.LatestPendingDelegations, x => x.Reason == "request-0");

        Assert.Equal(1, result.UserSummary.RoleCounts.Single(x => x.Role == nameof(UserRole.Admin)).Count);
        Assert.Equal(2, result.UserSummary.RoleCounts.Single(x => x.Role == nameof(UserRole.Staff)).Count);
        Assert.Equal(1, result.UserSummary.RoleCounts.Single(x => x.Role == nameof(UserRole.QCTransport)).Count);

        Assert.Equal(1, result.UserSummary.StaffByWorkshop.Single(x => x.WorkshopId == 1).ActiveStaffCount);
        Assert.Equal(1, result.UserSummary.StaffByWorkshop.Single(x => x.WorkshopId == 2).ActiveStaffCount);

        var staff1 = result.UserSummary.StaffWorkStatuses.Single(x => x.StaffId == 2);
        Assert.True(staff1.HasActiveWork);
        Assert.Equal(2, staff1.ActiveWorkCount);

        var staff2 = result.UserSummary.StaffWorkStatuses.Single(x => x.StaffId == 8);
        Assert.False(staff2.HasActiveWork);
        Assert.Equal(0, staff2.ActiveWorkCount);
        Assert.DoesNotContain(result.UserSummary.StaffWorkStatuses, x => x.StaffId == 9);
    }

    private static async Task<Batch> AddBatchAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        string batchNumber,
        BatchStatus status,
        DateTime endDate,
        DateTime? completedAt = null)
    {
        var batch = new Batch
        {
            BatchNumber = batchNumber,
            HatModelId = 1,
            Status = status,
            AssignedToLeadId = 1,
            TargetQuantity = 100,
            StartDate = DateTime.UtcNow.Date.AddDays(-5),
            EndDate = endDate,
            CompletedAt = completedAt
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }
}
