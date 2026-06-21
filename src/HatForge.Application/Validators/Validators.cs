using FluentValidation;
using HatForge.Application.DTOs;

namespace HatForge.Application.Validators;

public class CreateBatchValidator : AbstractValidator<CreateBatchDto>
{
    public CreateBatchValidator()
    {
        RuleFor(x => x.BatchNumber)
            .NotEmpty().WithMessage("Batch number is required")
            .MaximumLength(64).WithMessage("Batch number must not exceed 64 characters");

        RuleFor(x => x.HatModelId).GreaterThan(0).WithMessage("Valid hat model is required");
        RuleFor(x => x.TargetQuantity).GreaterThan(0).WithMessage("Target quantity must be greater than 0");
        RuleFor(x => x.AssignToLeadId).GreaterThan(0).WithMessage("Valid lead must be assigned");
        RuleFor(x => x.WorkshopIds).NotEmpty().WithMessage("At least one workshop must be selected");
    }
}

public class SubmitWorkValidator : AbstractValidator<SubmitWorkDto>
{
    public SubmitWorkValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0);
        RuleFor(x => x.WorkshopId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        RuleFor(x => x.PhotoUrl).NotEmpty().MaximumLength(512);
    }
}

public class RejectWorkValidator : AbstractValidator<RejectWorkDto>
{
    public RejectWorkValidator()
    {
        RuleFor(x => x.WorkId).GreaterThan(0);
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500);
    }
}

public class CreateTransferValidator : AbstractValidator<CreateTransferDto>
{
    public CreateTransferValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0);
        RuleFor(x => x.FromWorkshopId).GreaterThan(0);
        RuleFor(x => x.ToWorkshopId).GreaterThan(0).NotEqual(x => x.FromWorkshopId)
            .WithMessage("Destination workshop must be different from source");
    }
}

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
