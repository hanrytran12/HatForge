using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class TransferService : ITransferService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public TransferService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<TransferRequestDto> CreateTransferRequestAsync(CreateTransferDto dto)
    {
        var batch = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId)
            ?? throw new NotFoundException("Batch not found");

        var fromBw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.FromWorkshopId)
            ?? throw new NotFoundException("Source workshop not in batch");
        var toBw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.ToWorkshopId)
            ?? throw new NotFoundException("Destination workshop not in batch");

        if (!fromBw.IsCompleted)
            throw new BusinessRuleException("Source workshop has not completed work");

        if (toBw.OrderIndex != fromBw.OrderIndex + 1)
            throw new BusinessRuleException("Destination workshop must be the next in chain");

        var transfer = new TransferRequest
        {
            BatchId = dto.BatchId,
            FromWorkshopId = dto.FromWorkshopId,
            ToWorkshopId = dto.ToWorkshopId,
            Status = TransferStatus.Pending
        };

        await _unitOfWork.TransferRequests.AddAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyTransferRequestedAsync(new { TransferId = transfer.Id, BatchId = dto.BatchId });

        return await MapToDto(transfer.Id);
    }

    public async Task<TransferRequestDto> ApproveTransferAsync(ApproveTransferDto dto, int leadId)
    {
        var lead = await _unitOfWork.Users.GetByIdAsync(leadId)
            ?? throw new NotFoundException("Lead not found");
        if (lead.Role != UserRole.Lead)
            throw new ForbiddenException("Only Lead can approve transfers");

        var transfer = await _unitOfWork.TransferRequests.GetByIdAsync(dto.TransferId)
            ?? throw new NotFoundException("Transfer request not found");

        if (transfer.Status != TransferStatus.Pending)
            throw new BusinessRuleException("Transfer is not pending");

        transfer.Status = TransferStatus.Transferred;
        transfer.ApprovedByLeadId = leadId;
        transfer.ApprovedAt = DateTime.UtcNow;
        _unitOfWork.TransferRequests.Update(transfer);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyTransferApprovedAsync(transfer.BatchId, transfer.ToWorkshopId,
            new { TransferId = transfer.Id });

        await _notifications.NotifyWorkCanBeginAsync(transfer.ToWorkshopId, new
        {
            transfer.BatchId,
            TransferId = transfer.Id,
            ToWorkshopId = transfer.ToWorkshopId
        });

        return await MapToDto(transfer.Id);
    }

    public async Task<IReadOnlyList<TransferRequestDto>> GetPendingTransfersAsync()
    {
        var transfers = await _unitOfWork.TransferRequests.FindAsync(
            x => x.Status == TransferStatus.Pending,
            new[] { "Batch", "FromWorkshop", "ToWorkshop" });
        return transfers.Select(MapToDtoValue).ToList();
    }

    private async Task<TransferRequestDto> MapToDto(int id)
    {
        var t = await _unitOfWork.TransferRequests.FirstOrDefaultAsync(x => x.Id == id,
            new[] { "Batch", "FromWorkshop", "ToWorkshop" })
            ?? throw new NotFoundException("Transfer not found");
        return MapToDtoValue(t);
    }

    private static TransferRequestDto MapToDtoValue(TransferRequest t) => new(
        t.Id, t.BatchId, t.Batch?.BatchNumber ?? "",
        t.FromWorkshopId, t.FromWorkshop?.Name ?? "",
        t.ToWorkshopId, t.ToWorkshop?.Name ?? "",
        t.CreatedAt, t.ApprovedByLeadId, t.ApprovedAt, t.Status.ToString()
    );
}
