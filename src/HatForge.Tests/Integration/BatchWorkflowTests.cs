using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Integration;

public class BatchWorkflowTests
{
    [Fact]
    public async Task FullWorkflow_CreateSubmitApproveTransferComplete()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var notifications = new NoOpNotificationPublisher();
        var uow = TestDataFactory.CreateUnitOfWork(ctx);

        var batchService = new BatchService(uow, notifications);
        var workService = new WorkService(uow, notifications);
        var transferService = new TransferService(uow, notifications);

        // 1. Admin creates batch with 2-workshop chain
        var batch = await batchService.CreateBatchAsync(
            new CreateBatchDto("B-FLOW", 1, 100, new List<int> { 1, 2 }, 1));
        Assert.Equal(nameof(BatchStatus.Assigned), batch.Status);

        // 2. Staff submits work at workshop 1
        var work = await workService.SubmitWorkAsync(new SubmitWorkDto(batch.Id, 1, 100, "/uploads/w1.jpg"), staffId: 2);
        Assert.Equal(nameof(WorkStatus.Submitted), work.Status);

        // 3. QC approves work
        var approved = await workService.ApproveWorkAsync(work.Id, qcId: 3);
        Assert.Equal(nameof(WorkStatus.Approved), approved.Status);

        // 4. Lead marks workshop 1 complete -> batch ReadyForTransfer
        var afterW1 = await batchService.MarkWorkshopCompletedAsync(batch.Id, 1);
        Assert.Equal(nameof(BatchStatus.ReadyForTransfer), afterW1.Status);

        // 5. Lead creates + approves transfer to workshop 2
        var transfer = await transferService.CreateTransferRequestAsync(new CreateTransferDto(batch.Id, 1, 2));
        var approvedTransfer = await transferService.ApproveTransferAsync(new ApproveTransferDto(transfer.Id), leadId: 1);
        Assert.Equal(nameof(TransferStatus.Transferred), approvedTransfer.Status);

        // 6. Workshop 2 completes -> batch Completed
        var final = await batchService.MarkWorkshopCompletedAsync(batch.Id, 2);
        Assert.Equal(nameof(BatchStatus.Completed), final.Status);
        Assert.NotNull(final.CompletedAt);
    }

    [Fact]
    public async Task RejectionFlow_RejectedWorkRecordsReason()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        var notifications = new NoOpNotificationPublisher();
        var uow = TestDataFactory.CreateUnitOfWork(ctx);

        var batchService = new BatchService(uow, notifications);
        var workService = new WorkService(uow, notifications);

        var batch = await batchService.CreateBatchAsync(
            new CreateBatchDto("B-REJ", 1, 50, new List<int> { 1 }, 1));
        var work = await workService.SubmitWorkAsync(new SubmitWorkDto(batch.Id, 1, 50, "/uploads/x.jpg"), staffId: 2);

        var rejected = await workService.RejectWorkAsync(
            new RejectWorkDto(work.Id, "Material", "Wrong fabric color"), qcId: 3);

        Assert.Equal(nameof(WorkStatus.Rejected), rejected.Status);
        Assert.Equal("Material", rejected.RejectionReason);
        Assert.Equal("Wrong fabric color", rejected.RejectionNotes);
    }
}
