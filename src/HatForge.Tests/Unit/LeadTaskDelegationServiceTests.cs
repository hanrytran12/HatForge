using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Unit;

public class LeadTaskDelegationServiceTests
{
    private const int LeadId = 1;
    private const int QcTransportId = 7;
    private const int OtherTransportId = 8;
    private const int AdminId = 4;

    private static LeadTaskDelegationService CreateService(HatForge.Infrastructure.Data.AppDbContext ctx)
        => new(TestDataFactory.CreateUnitOfWork(ctx), new NoOpNotificationPublisher());

    private static async Task<int> SeedMaterialDeliveryAsync(HatForge.Infrastructure.Data.AppDbContext ctx)
    {
        var batch = new Batch
        {
            BatchNumber = "B-DELEGATE-MAT",
            HatModelId = 1,
            TargetQuantity = 100,
            Status = BatchStatus.InProduction,
            AssignedToLeadId = LeadId,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        var delivery = new MaterialDelivery
        {
            BatchId = batch.Id,
            WorkshopId = 1,
            ScheduledDate = DateTime.UtcNow,
            Status = MaterialDeliveryStatus.Scheduled
        };
        ctx.MaterialDeliveries.Add(delivery);
        await ctx.SaveChangesAsync();

        ctx.MaterialDeliveryItems.Add(new MaterialDeliveryItem
        {
            MaterialDeliveryId = delivery.Id,
            MaterialName = "Wool Felt",
            PlannedQuantity = 100
        });
        await ctx.SaveChangesAsync();

        return delivery.Id;
    }

    private static async Task<int> SeedTransferAsync(HatForge.Infrastructure.Data.AppDbContext ctx)
    {
        var batch = new Batch
        {
            BatchNumber = "B-DELEGATE-TR",
            HatModelId = 1,
            TargetQuantity = 100,
            Status = BatchStatus.ReadyForTransfer,
            AssignedToLeadId = LeadId,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        var transfer = new TransferRequest
        {
            BatchId = batch.Id,
            FromWorkshopId = 1,
            ToWorkshopId = 2,
            CreatedByQCId = 3,
            Status = TransferStatus.Pending
        };
        ctx.TransferRequests.Add(transfer);
        await ctx.SaveChangesAsync();

        return transfer.Id;
    }

    private static async Task<int> SeedFinalReviewBatchAsync(
        HatForge.Infrastructure.Data.AppDbContext ctx,
        BatchStatus status = BatchStatus.PendingLeadReview)
    {
        var batch = new Batch
        {
            BatchNumber = $"B-FINAL-{Guid.NewGuid():N}".Substring(0, 14),
            HatModelId = 1,
            TargetQuantity = 100,
            Status = status,
            AssignedToLeadId = LeadId,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        ctx.Batches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.BatchWorkshops.Add(new BatchWorkshop
        {
            BatchId = batch.Id,
            WorkshopId = 1,
            OrderIndex = 0,
            RequiresMaterials = false,
            MaterialsReceived = true,
            IsCompleted = true,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(10)
        });
        await ctx.SaveChangesAsync();

        return batch.Id;
    }

    private static async Task SeedTransportUsersAsync(HatForge.Infrastructure.Data.AppDbContext ctx)
    {
        ctx.Users.Add(TestDataFactory.QcTransport(QcTransportId));
        ctx.Users.Add(TestDataFactory.QcTransport(OtherTransportId));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateMaterialDeliveryDelegation_ByAssignedLead_CreatesPendingRequest()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var deliveryId = await SeedMaterialDeliveryAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.MaterialDelivery,
                deliveryId,
                QcTransportId,
                "Lead is busy"),
            LeadId);

        Assert.Equal(LeadTaskDelegationType.MaterialDelivery, result.Type);
        Assert.Equal(LeadTaskDelegationStatus.PendingAdminApproval, result.Status);
        Assert.Equal(deliveryId, result.MaterialDeliveryId);
        Assert.Equal(QcTransportId, result.AssignedTransportQcId);
    }

    [Fact]
    public async Task CreateMaterialDeliveryDelegation_ByWrongLead_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        ctx.Users.Add(TestDataFactory.Lead(id: 9));
        await SeedTransportUsersAsync(ctx);
        var deliveryId = await SeedMaterialDeliveryAsync(ctx);
        var service = CreateService(ctx);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(
                new CreateLeadTaskDelegationDto(
                    LeadTaskDelegationType.MaterialDelivery,
                    deliveryId,
                    QcTransportId,
                    null),
                leadId: 9));
    }

    [Fact]
    public async Task MarkMaterialDelivered_AfterAdminApproval_CompletesDelegationAndMarksDeliveryDelivered()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var deliveryId = await SeedMaterialDeliveryAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.MaterialDelivery,
                deliveryId,
                QcTransportId,
                null),
            LeadId);
        await service.ApproveAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto("ok"));

        var completed = await service.MarkMaterialDeliveredAsync(created.Id, QcTransportId);

        Assert.Equal(LeadTaskDelegationStatus.Completed, completed.Status);
        var delivery = await TestDataFactory.CreateUnitOfWork(ctx).MaterialDeliveries.GetByIdAsync(deliveryId);
        Assert.Equal(MaterialDeliveryStatus.Delivered, delivery!.Status);
        Assert.False(delivery.IsConfirmed);
    }

    [Fact]
    public async Task MarkMaterialDelivered_BeforeAdminApproval_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var deliveryId = await SeedMaterialDeliveryAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.MaterialDelivery,
                deliveryId,
                QcTransportId,
                null),
            LeadId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.MarkMaterialDeliveredAsync(created.Id, QcTransportId));
    }

    [Fact]
    public async Task MarkMaterialDelivered_ByWrongTransportQc_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var deliveryId = await SeedMaterialDeliveryAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.MaterialDelivery,
                deliveryId,
                QcTransportId,
                null),
            LeadId);
        await service.ApproveAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto(null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.MarkMaterialDeliveredAsync(created.Id, OtherTransportId));
    }

    [Fact]
    public async Task TransferDelegation_ApprovedByAdmin_AllowsTransportQcToApproveTransfer()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var transferId = await SeedTransferAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.TransferApproval,
                transferId,
                QcTransportId,
                "Need someone to inspect transfer"),
            LeadId);
        await service.ApproveAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto("go"));

        var completed = await service.ApproveDelegatedTransferAsync(created.Id, QcTransportId);

        Assert.Equal(LeadTaskDelegationStatus.Completed, completed.Status);
        var transfer = await TestDataFactory.CreateUnitOfWork(ctx).TransferRequests.GetByIdAsync(transferId);
        Assert.Equal(TransferStatus.Approved, transfer!.Status);
        Assert.Equal(LeadId, transfer.ApprovedByLeadId);
        Assert.NotNull(transfer.ApprovedAt);
    }

    [Fact]
    public async Task FinalReviewDelegation_ApprovedByAdmin_AllowsTransportQcToApproveFinalReview()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var batchId = await SeedFinalReviewBatchAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.FinalReview,
                batchId,
                QcTransportId,
                "Lead cannot attend final review"),
            LeadId);
        await service.ApproveAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto("ok"));

        var completed = await service.ApproveDelegatedFinalReviewAsync(created.Id, QcTransportId);

        Assert.Equal(LeadTaskDelegationType.FinalReview, completed.Type);
        Assert.Equal(LeadTaskDelegationStatus.Completed, completed.Status);
        Assert.Null(completed.MaterialDeliveryId);
        Assert.Null(completed.TransferRequestId);
        var batch = await TestDataFactory.CreateUnitOfWork(ctx).Batches.GetByIdAsync(batchId);
        Assert.Equal(BatchStatus.PendingGateQC, batch!.Status);
    }

    [Fact]
    public async Task CreateFinalReviewDelegation_WhenBatchNotPendingLeadReview_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var batchId = await SeedFinalReviewBatchAsync(ctx, BatchStatus.InProduction);
        var service = CreateService(ctx);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(
                new CreateLeadTaskDelegationDto(
                    LeadTaskDelegationType.FinalReview,
                    batchId,
                    QcTransportId,
                    null),
                LeadId));
    }

    [Fact]
    public async Task CreateFinalReviewDelegation_WhenActiveDelegationExists_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var batchId = await SeedFinalReviewBatchAsync(ctx);
        var service = CreateService(ctx);
        var dto = new CreateLeadTaskDelegationDto(
            LeadTaskDelegationType.FinalReview,
            batchId,
            QcTransportId,
            null);

        await service.CreateAsync(dto, LeadId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(dto, LeadId));
    }

    [Fact]
    public async Task CreateTransferDelegation_WhenActiveDelegationExists_ThrowsBusinessRule()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var transferId = await SeedTransferAsync(ctx);
        var service = CreateService(ctx);
        var dto = new CreateLeadTaskDelegationDto(
            LeadTaskDelegationType.TransferApproval,
            transferId,
            QcTransportId,
            null);

        await service.CreateAsync(dto, LeadId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(dto, LeadId));
    }

    [Fact]
    public async Task RejectDelegation_ByAdmin_NotifiesLeadAndPreventsExecution()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var transferId = await SeedTransferAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.TransferApproval,
                transferId,
                QcTransportId,
                null),
            LeadId);
        var rejected = await service.RejectAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto("no"));

        Assert.Equal(LeadTaskDelegationStatus.Rejected, rejected.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ApproveDelegatedTransferAsync(created.Id, QcTransportId));
    }

    [Fact]
    public async Task GetRequestedByLead_IncludesRejectedRequests()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var transferId = await SeedTransferAsync(ctx);
        var service = CreateService(ctx);

        var created = await service.CreateAsync(
            new CreateLeadTaskDelegationDto(
                LeadTaskDelegationType.TransferApproval,
                transferId,
                QcTransportId,
                null),
            LeadId);
        await service.RejectAsync(created.Id, AdminId, new ReviewLeadTaskDelegationDto("no"));

        var requests = await service.GetRequestedByLeadAsync(LeadId);

        var request = Assert.Single(requests);
        Assert.Equal(created.Id, request.Id);
        Assert.Equal(LeadTaskDelegationStatus.Rejected, request.Status);
        Assert.Equal("no", request.AdminNotes);
    }

    [Fact]
    public async Task GetRequestedByLead_WhenActorIsNotLead_ThrowsForbidden()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        await SeedTransportUsersAsync(ctx);
        var service = CreateService(ctx);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetRequestedByLeadAsync(AdminId));
    }
}
