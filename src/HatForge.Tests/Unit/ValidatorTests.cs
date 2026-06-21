using FluentValidation.TestHelper;
using HatForge.Application.DTOs;
using HatForge.Application.Validators;
using Xunit;

namespace HatForge.Tests.Unit;

public class ValidatorTests
{
    [Fact]
    public void CreateBatch_EmptyNumber_Invalid()
    {
        var v = new CreateBatchValidator();
        var result = v.TestValidate(new CreateBatchDto("", 1, 10, new List<int> { 1 }, 1));
        result.ShouldHaveValidationErrorFor(x => x.BatchNumber);
    }

    [Fact]
    public void CreateBatch_NoWorkshops_Invalid()
    {
        var v = new CreateBatchValidator();
        var result = v.TestValidate(new CreateBatchDto("B-1", 1, 10, new List<int>(), 1));
        result.ShouldHaveValidationErrorFor(x => x.WorkshopIds);
    }

    [Fact]
    public void CreateBatch_Valid_Passes()
    {
        var v = new CreateBatchValidator();
        var result = v.TestValidate(new CreateBatchDto("B-1", 1, 10, new List<int> { 1 }, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SubmitWork_ZeroQuantity_Invalid()
    {
        var v = new SubmitWorkValidator();
        var result = v.TestValidate(new SubmitWorkDto(1, 1, 0, "/p.jpg"));
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void SubmitWork_NoPhoto_Invalid()
    {
        var v = new SubmitWorkValidator();
        var result = v.TestValidate(new SubmitWorkDto(1, 1, 5, ""));
        result.ShouldHaveValidationErrorFor(x => x.PhotoUrl);
    }

    [Fact]
    public void RejectWork_NoReason_Invalid()
    {
        var v = new RejectWorkValidator();
        var result = v.TestValidate(new RejectWorkDto(1, "", "notes"));
        result.ShouldHaveValidationErrorFor(x => x.RejectionReason);
    }

    [Fact]
    public void CreateTransfer_SameWorkshop_Invalid()
    {
        var v = new CreateTransferValidator();
        var result = v.TestValidate(new CreateTransferDto(1, 1, 1));
        result.ShouldHaveValidationErrorFor(x => x.ToWorkshopId);
    }
}
