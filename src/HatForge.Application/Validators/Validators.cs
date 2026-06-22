using FluentValidation;
using HatForge.Application.DTOs;

namespace HatForge.Application.Validators;

public class CreateBatchValidator : AbstractValidator<CreateBatchDto>
{
    public CreateBatchValidator()
    {
        RuleFor(x => x.HatModelId).GreaterThan(0).WithMessage("Valid hat model is required");
        RuleFor(x => x.TargetQuantity).GreaterThan(0).WithMessage("Target quantity must be greater than 0");
        RuleFor(x => x.AssignToLeadId).GreaterThan(0).WithMessage("Valid lead must be assigned");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Start date is required");
        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");
    }
}

public class PlanBatchValidator : AbstractValidator<PlanBatchDto>
{
    public PlanBatchValidator()
    {
        RuleFor(x => x.Workshops)
            .NotEmpty().WithMessage("At least one workshop must be in the plan");

        RuleForEach(x => x.Workshops).ChildRules(w =>
        {
            w.RuleFor(x => x.WorkshopId).GreaterThan(0).WithMessage("Valid workshop is required");
            w.RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
            w.RuleFor(x => x.StartDate).NotEmpty();
            w.RuleFor(x => x.EndDate)
                .NotEmpty()
                .GreaterThan(x => x.StartDate).WithMessage("Workshop end date must be after start date");
            w.When(x => x.RequiresMaterials, () =>
            {
                w.RuleFor(x => x.MaterialDeliveryDate)
                    .NotNull().WithMessage("MaterialDeliveryDate is required when workshop requires materials");
                w.RuleFor(x => x.MaterialItems)
                    .NotEmpty().WithMessage("At least one material item is required when workshop requires materials");
                w.RuleForEach(x => x.MaterialItems).ChildRules(m =>
                {
                    m.RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(256);
                    m.RuleFor(x => x.PlannedQuantity).GreaterThan(0);
                });
            });
        });
    }
}

public class SubmitWorkValidator : AbstractValidator<SubmitWorkDto>
{
    public SubmitWorkValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0);
        RuleFor(x => x.WorkshopId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        RuleFor(x => x.PhotoUrls).NotEmpty().WithMessage("At least one photo is required");
        RuleForEach(x => x.PhotoUrls).NotEmpty().MaximumLength(512);
    }
}

public class RejectWorkValidator : AbstractValidator<RejectWorkDto>
{
    public RejectWorkValidator()
    {
        RuleFor(x => x.WorkId).GreaterThan(0);
        RuleFor(x => x.RejectionNotes).NotEmpty().MaximumLength(500);
    }
}

public class CreateTransferValidator : AbstractValidator<CreateTransferDto>
{
    public CreateTransferValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0);
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
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
