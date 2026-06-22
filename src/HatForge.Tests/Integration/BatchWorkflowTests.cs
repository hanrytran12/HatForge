using HatForge.Application.DTOs;
using HatForge.Application.Services;
using HatForge.Domain.Enums;
using HatForge.Tests.Fixtures;
using Xunit;

namespace HatForge.Tests.Integration;

public class BatchWorkflowTests
{
    private static readonly DateTime Start = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

    private static WorkshopPlanItemDto NoMaterial(int workshopId, int order, DateTime start, DateTime end)
        => new(workshopId, order, false, start, end, null, null);

    [Fact]
    public async Task FullWorkflow_CreatePlanSubmitApproveTransferComplete()
    {
        using var ctx = TestDataFactory.CreateContext();
        await TestDataFactory.SeedBaseAsync(ctx);
        // QC for the next workshop (workshop 2) who will confirm receipt
        ctx.Users.Add(TestDataFactory.QcWorkshop(id: 5, workshopId: 2));
        await ctx.SaveChangesAsync();
        var notifications = new NoOpNotificationPublisher();
        var uow = TestDataFactory.CreateUnitOfWork(ctx);

        var batchService = new BatchService(uow, notifications);
        var workService = new WorkService(uow, notifications);
        var transferService = new TransferService(uow, notifications);

        // 1. Admin creates batch — no workshops yet
        var batch = await batchService.CreateBatchAsync(
            new CreateBatchDto(1, 100, Start, End, 1));
        Assert.Equal(nameof(BatchStatus.Assigned), batch.Status);
        Assert.Empty(batch.Workshops);

        // 2. Lead plans workshop chain (no materials needed)
        var plan = new PlanBatchDto(new List<WorkshopPlanItemDto>
        {
            NoMaterial(1, 0, Start, Start.AddDays(10)),
            NoMaterial(2, 1, Start.AddDays(11), End)
        });
        var planned = await batchService.PlanBatchAsync(batch.Id, plan, 1);
        Assert.Equal(nameof(BatchStatus.InProduction), planned.Status);
        Assert.Equal(2, planned.Workshops.Count);

        // 3. Staff submits work at workshop 1
        var work = await workService.SubmitWorkAsync(
            new SubmitWorkDto(batch.Id, 1, 100, new List<string> { "/uploads/w1.jpg" }), staffId: 2);
        Assert.Equal(nameof(WorkStatus.Submitted), work.Status);

        // 4. QC approves work
        var approved = await workService.ApproveWorkAsync(work.Id, qcId: 3);
        Assert.Equal(nameof(WorkStatus.Approved), approved.Status);

        // 5. QC of workshop 1 creates a transfer request → server auto-determines next workshop
        var transfer = await transferService.CreateTransferRequestAsync(
            new CreateTransferDto(batch.Id), qcId: 3);
        Assert.Equal(nameof(TransferStatus.Pending), transfer.Status);

        // 6. Lead approves the transfer
        var approvedTransfer = await transferService.ApproveTransferAsync(
            new ApproveTransferDto(transfer.Id), 1);
        Assert.Equal(nameof(TransferStatus.Approved), approvedTransfer.Status);

        // 7. QC of workshop 2 confirms receipt → workshop 1 marked completed
        var received = await transferService.ConfirmReceiptAsync(
            new ConfirmReceiptDto(transfer.Id), qcId: 5);
        Assert.Equal(nameof(TransferStatus.Transferred), received.Status);

        // 8. Workshop 2 (last in chain) completes → batch Completed
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
            new CreateBatchDto(1, 50, Start, End, 1));
        await batchService.PlanBatchAsync(batch.Id,
            new PlanBatchDto(new List<WorkshopPlanItemDto>
            {
                NoMaterial(1, 0, Start, End)
            }), 1);

        var work = await workService.SubmitWorkAsync(
            new SubmitWorkDto(batch.Id, 1, 50, new List<string> { "/uploads/x.jpg" }), staffId: 2);

        var rejected = await workService.RejectWorkAsync(
            new RejectWorkDto(work.Id, "Wrong fabric color", new List<string>()), qcId: 3);

        Assert.Equal(nameof(WorkStatus.Rejected), rejected.Status);
        Assert.Equal("Wrong fabric color", rejected.RejectionNotes);
    }
}
