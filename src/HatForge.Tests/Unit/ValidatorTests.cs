using FluentValidation.TestHelper;
using HatForge.Application.DTOs;
using HatForge.Application.Validators;
using Xunit;

namespace HatForge.Tests.Unit;

public class ValidatorTests
{
    private static readonly DateTime Start = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateBatch_EndBeforeStart_Invalid()
    {
        var v = new CreateBatchValidator();
        var result = v.TestValidate(new CreateBatchDto(1, 10, End, Start, 1));
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public void CreateBatch_Valid_Passes()
    {
        var v = new CreateBatchValidator();
        var result = v.TestValidate(new CreateBatchDto(1, 10, Start, End, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PlanBatch_NoWorkshops_Invalid()
    {
        var v = new PlanBatchValidator();
        var result = v.TestValidate(new PlanBatchDto(new List<WorkshopPlanItemDto>()));
        result.ShouldHaveValidationErrorFor(x => x.Workshops);
    }

    [Fact]
    public void PlanBatch_RequiresMaterials_NoDeliveryDate_Invalid()
    {
        var v = new PlanBatchValidator();
        var item = new WorkshopPlanItemDto(1, 0, true, Start, End, null, null);
        var result = v.TestValidate(new PlanBatchDto(new List<WorkshopPlanItemDto> { item }));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void PlanBatch_Valid_Passes()
    {
        var v = new PlanBatchValidator();
        var item = new WorkshopPlanItemDto(1, 0, true, Start, End, Start.AddDays(-1),
            new List<MaterialItemDto> { new("Wool Felt", 500) },
            EstimatedMetersPerUnit: 2m);
        var result = v.TestValidate(new PlanBatchDto(new List<WorkshopPlanItemDto> { item }));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SubmitWork_ZeroQuantity_Invalid()
    {
        var v = new SubmitWorkValidator();
        var result = v.TestValidate(new SubmitWorkDto(1, 1, 0, new List<string> { "/p.jpg" }));
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void SubmitWork_NoPhoto_Invalid()
    {
        var v = new SubmitWorkValidator();
        var result = v.TestValidate(new SubmitWorkDto(1, 1, 5, new List<string>()));
        result.ShouldHaveValidationErrorFor(x => x.PhotoUrls);
    }

    [Fact]
    public void RejectWork_NoNotes_Invalid()
    {
        var v = new RejectWorkValidator();
        var result = v.TestValidate(new RejectWorkDto(1, "", true, 0m, new List<string>()));
        result.ShouldHaveValidationErrorFor(x => x.RejectionNotes);
    }

    [Fact]
    public void CreateTransfer_NoBatchId_Invalid()
    {
        var v = new CreateTransferValidator();
        var result = v.TestValidate(new CreateTransferDto(0));
        result.ShouldHaveValidationErrorFor(x => x.BatchId);
    }
}
