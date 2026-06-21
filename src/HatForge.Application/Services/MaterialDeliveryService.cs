using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class MaterialDeliveryService : IMaterialDeliveryService
{
    private readonly IUnitOfWork _unitOfWork;

    public MaterialDeliveryService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<MaterialDeliveryDto> ScheduleDeliveryAsync(CreateMaterialDeliveryDto dto)
    {
        _ = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId)
            ?? throw new NotFoundException("Batch not found");
        var bw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.WorkshopId)
            ?? throw new NotFoundException("Workshop not in batch");

        var delivery = new MaterialDelivery
        {
            BatchId = dto.BatchId,
            WorkshopId = dto.WorkshopId,
            ScheduledDate = dto.ScheduledDate,
            DeliveredQuantity = dto.DeliveredQuantity,
            Status = MaterialDeliveryStatus.Scheduled
        };

        await _unitOfWork.MaterialDeliveries.AddAsync(delivery);
        await _unitOfWork.SaveChangesAsync();

        return await MapToDto(delivery.Id);
    }

    public async Task<MaterialDeliveryDto> ConfirmDeliveryAsync(ConfirmMaterialDeliveryDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can confirm material receipt");

        var delivery = await _unitOfWork.MaterialDeliveries.GetByIdAsync(dto.DeliveryId)
            ?? throw new NotFoundException("Delivery not found");

        delivery.IsConfirmed = true;
        delivery.DeliveredQuantity = dto.DeliveredQuantity;
        delivery.ConfirmedByQCId = qcId;
        delivery.ConfirmedAt = DateTime.UtcNow;
        delivery.DeliveredDate = DateTime.UtcNow;
        delivery.Status = MaterialDeliveryStatus.Received;
        _unitOfWork.MaterialDeliveries.Update(delivery);

        var bw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == delivery.BatchId && x.WorkshopId == delivery.WorkshopId);
        if (bw != null)
        {
            bw.MaterialsReceived = true;
            _unitOfWork.BatchWorkshops.Update(bw);
        }

        await _unitOfWork.SaveChangesAsync();
        return await MapToDto(delivery.Id);
    }

    private async Task<MaterialDeliveryDto> MapToDto(int id)
    {
        var d = await _unitOfWork.MaterialDeliveries.FirstOrDefaultAsync(x => x.Id == id,
            new[] { "Workshop" })
            ?? throw new NotFoundException("Delivery not found");
        return new MaterialDeliveryDto(
            d.Id, d.BatchId, d.WorkshopId, d.Workshop?.Name ?? "",
            d.ScheduledDate, d.DeliveredDate, d.DeliveredQuantity,
            d.IsConfirmed, d.Status.ToString());
    }
}
