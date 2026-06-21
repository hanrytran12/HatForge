using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class BatchService : IBatchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public BatchService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<BatchDto> CreateBatchAsync(CreateBatchDto dto)
    {
        var existing = await _unitOfWork.Batches.FirstOrDefaultAsync(x => x.BatchNumber == dto.BatchNumber);
        if (existing != null)
            throw new BusinessRuleException("Batch number already exists");

        var hatModel = await _unitOfWork.HatModels.GetByIdAsync(dto.HatModelId)
            ?? throw new NotFoundException("HatModel not found");
        var lead = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Id == dto.AssignToLeadId && x.Role == UserRole.Lead)
            ?? throw new NotFoundException("Lead not found");

        if (dto.WorkshopIds == null || dto.WorkshopIds.Count == 0)
            throw new BusinessRuleException("At least one workshop must be selected");

        var batch = new Batch
        {
            BatchNumber = dto.BatchNumber,
            HatModelId = dto.HatModelId,
            TargetQuantity = dto.TargetQuantity,
            Status = BatchStatus.Assigned,
            AssignedToLeadId = dto.AssignToLeadId
        };

        await _unitOfWork.Batches.AddAsync(batch);
        await _unitOfWork.SaveChangesAsync();

        for (int i = 0; i < dto.WorkshopIds.Count; i++)
        {
            var bw = new BatchWorkshop
            {
                BatchId = batch.Id,
                WorkshopId = dto.WorkshopIds[i],
                OrderIndex = i,
                MaterialsReceived = false,
                IsCompleted = false
            };
            await _unitOfWork.BatchWorkshops.AddAsync(bw);
        }

        await _unitOfWork.SaveChangesAsync();
        await _notifications.NotifyBatchCreatedAsync(new { BatchId = batch.Id, batch.BatchNumber });
        return await GetBatchByIdAsync(batch.Id) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchDto?> GetBatchByIdAsync(int id)
    {
        var batch = await _unitOfWork.Batches.FirstOrDefaultAsync(
            x => x.Id == id,
            new[] { "HatModel", "AssignedToLead", "BatchWorkshops.Workshop" });

        if (batch == null) return null;

        return MapToDto(batch);
    }

    public async Task<IReadOnlyList<BatchListDto>> GetAllBatchesAsync()
    {
        var batches = await _unitOfWork.Batches.FindAsync(x => true);
        return batches
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BatchListDto(x.Id, x.BatchNumber, x.HatModel.Name, x.Status.ToString(), x.CreatedAt))
            .ToList();
    }

    public async Task<BatchDto> AssignLeadAsync(int batchId, int leadId)
    {
        var batch = await _unitOfWork.Batches.GetByIdAsync(batchId)
            ?? throw new NotFoundException("Batch not found");
        var lead = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Id == leadId && x.Role == UserRole.Lead)
            ?? throw new NotFoundException("Lead not found");

        batch.AssignedToLeadId = leadId;
        batch.Status = BatchStatus.Assigned;
        _unitOfWork.Batches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        return await GetBatchByIdAsync(batchId) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchDto> MarkWorkshopCompletedAsync(int batchId, int workshopId)
    {
        var bw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == batchId && x.WorkshopId == workshopId)
            ?? throw new NotFoundException("BatchWorkshop not found");

        bw.IsCompleted = true;
        _unitOfWork.BatchWorkshops.Update(bw);

        var allCompleted = (await _unitOfWork.BatchWorkshops.FindAsync(x => x.BatchId == batchId))
            .All(x => x.IsCompleted);

        var batch = await _unitOfWork.Batches.GetByIdAsync(batchId)
            ?? throw new NotFoundException("Batch not found");

        if (allCompleted)
        {
            batch.Status = BatchStatus.Completed;
            batch.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            batch.Status = BatchStatus.ReadyForTransfer;
        }

        _unitOfWork.Batches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        if (allCompleted)
            await _notifications.NotifyBatchCompletedAsync(batchId, new { BatchId = batchId });

        return await GetBatchByIdAsync(batchId) ?? throw new NotFoundException("Batch not found");
    }

    private static BatchDto MapToDto(Batch batch) => new(
        batch.Id,
        batch.BatchNumber,
        batch.HatModelId,
        batch.HatModel?.Name ?? "",
        batch.Status.ToString(),
        batch.AssignedToLeadId,
        batch.AssignedToLead?.Name,
        batch.TargetQuantity,
        batch.CreatedAt,
        batch.CompletedAt,
        batch.BatchWorkshops?
            .OrderBy(x => x.OrderIndex)
            .Select(x => new WorkshopInBatchDto(x.WorkshopId, x.Workshop?.Name ?? "", x.OrderIndex, x.MaterialsReceived, x.IsCompleted))
            .ToList() ?? new()
    );
}
