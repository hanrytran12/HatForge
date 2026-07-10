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

public class CreateHatModelValidator : AbstractValidator<CreateHatModelDto>
{
    public CreateHatModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hat model name is required")
            .MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateHatModelValidator : AbstractValidator<UpdateHatModelDto>
{
    public UpdateHatModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hat model name is required")
            .MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(500);
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
                w.RuleFor(x => x.EstimatedMetersPerUnit)
                    .GreaterThan(0)
                    .WithMessage("EstimatedMetersPerUnit must be greater than 0 when workshop requires materials");
                w.RuleForEach(x => x.MaterialItems).ChildRules(m =>
                {
                    m.RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(256);
                    m.RuleFor(x => x.Unit).NotEmpty().MaximumLength(32);
                    m.RuleFor(x => x.PlannedQuantity).GreaterThan(0);
                });
            });

            w.When(x => !x.RequiresMaterials, () =>
            {
                w.RuleFor(x => x.MaterialDeliveryDate)
                    .Null().WithMessage("MaterialDeliveryDate must be omitted when workshop does not require materials");
                w.RuleFor(x => x.MaterialItems)
                    .Must(x => x == null || x.Count == 0)
                    .WithMessage("MaterialItems must be omitted when workshop does not require materials");
                w.RuleFor(x => x.EstimatedMetersPerUnit)
                    .Equal(0)
                    .WithMessage("EstimatedMetersPerUnit must be 0 when workshop does not require materials");
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
        RuleFor(x => x.ReportedMaterialUsed)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ReportedMaterialUsed.HasValue)
            .WithMessage("ReportedMaterialUsed must be greater than or equal to 0");
    }
}

public class RejectWorkValidator : AbstractValidator<RejectWorkDto>
{
    public RejectWorkValidator()
    {
        RuleFor(x => x.WorkId).GreaterThan(0);
        RuleFor(x => x.RejectionNotes).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PassedQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("PassedQuantity must be greater than or equal to 0");
        RuleFor(x => x.RepairableQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("RepairableQuantity must be greater than or equal to 0");
        RuleFor(x => x.UnrepairableQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("UnrepairableQuantity must be greater than or equal to 0");
        RuleFor(x => x.ActualMaterialUsed)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ActualMaterialUsed must be greater than or equal to 0");
    }
}

public class ApproveWorkValidator : AbstractValidator<ApproveWorkDto>
{
    public ApproveWorkValidator()
    {
        RuleFor(x => x.WorkId).GreaterThan(0);
        RuleFor(x => x.ActualMaterialUsed)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ActualMaterialUsed must be greater than or equal to 0");
    }
}

public class CreateTransferValidator : AbstractValidator<CreateTransferDto>
{
    public CreateTransferValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0);
    }
}

public class ConfirmReceiptValidator : AbstractValidator<ConfirmReceiptDto>
{
    public ConfirmReceiptValidator()
    {
        RuleFor(x => x.TransferId).GreaterThan(0);
        RuleFor(x => x.ReceivedUsableQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ReceivedUsableQuantity must be greater than or equal to 0");
        RuleFor(x => x.ReceivedDefectiveQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ReceivedDefectiveQuantity must be greater than or equal to 0");
        RuleFor(x => x.ReceiptInspectionNotes).MaximumLength(500);
    }
}

public class ConfirmMaterialDeliveryValidator : AbstractValidator<ConfirmMaterialDeliveryDto>
{
    public ConfirmMaterialDeliveryValidator()
    {
        RuleFor(x => x.DeliveryId).GreaterThan(0);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item must be confirmed");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ItemId).GreaterThan(0);
            i.RuleFor(x => x.ActualQuantity).GreaterThanOrEqualTo(0)
                .WithMessage("Actual quantity must be greater than or equal to 0");
        });
    }
}

public class ConfirmMaterialRequestValidator : AbstractValidator<ConfirmMaterialRequestDto>
{
    public ConfirmMaterialRequestValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item must be confirmed");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ItemId).GreaterThan(0);
            i.RuleFor(x => x.ActualQuantity).GreaterThanOrEqualTo(0)
                .WithMessage("Actual quantity must be greater than or equal to 0");
        });
    }
}

public class CreateAdHocMaterialRequestValidator : AbstractValidator<CreateAdHocMaterialRequestDto>
{
    public CreateAdHocMaterialRequestValidator()
    {
        RuleFor(x => x.BatchId).GreaterThan(0);
        RuleFor(x => x.WorkshopId).GreaterThan(0);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required")
            .MaximumLength(500);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item must be requested");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.MaterialName)
                .NotEmpty().MaximumLength(256);
            i.RuleFor(x => x.Unit)
                .NotEmpty().MaximumLength(32);
            i.RuleFor(x => x.RequestedQuantity)
                .GreaterThan(0)
                .WithMessage("Requested quantity must be greater than 0");
        });
    }
}

public class StockInLeadMaterialValidator : AbstractValidator<StockInLeadMaterialDto>
{
    public StockInLeadMaterialValidator()
    {
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class AdjustLeadMaterialStockValidator : AbstractValidator<AdjustLeadMaterialStockDto>
{
    public AdjustLeadMaterialStockValidator()
    {
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(32);
        RuleFor(x => x.NewQuantityOnHand)
            .GreaterThanOrEqualTo(0)
            .WithMessage("NewQuantityOnHand must be greater than or equal to 0");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class CreateLeadTaskDelegationValidator : AbstractValidator<CreateLeadTaskDelegationDto>
{
    public CreateLeadTaskDelegationValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.TaskId).GreaterThan(0);
        RuleFor(x => x.AssignedTransportQcId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class ReviewLeadTaskDelegationValidator : AbstractValidator<ReviewLeadTaskDelegationDto>
{
    public ReviewLeadTaskDelegationValidator()
    {
        RuleFor(x => x.AdminNotes).MaximumLength(500);
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
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
