using HatForge.Application.DTOs;
using HatForge.Domain.Entities;

namespace HatForge.Application.Interfaces;

public interface IBatchService
{
    Task<BatchDto> CreateBatchAsync(CreateBatchDto dto);
    Task<BatchDto> PlanBatchAsync(int batchId, PlanBatchDto dto, int leadId);
    Task<BatchDto?> GetBatchByIdAsync(int id);
    Task<IReadOnlyList<BatchListDto>> GetAllBatchesAsync();
    Task<BatchDto> MarkWorkshopCompletedAsync(int batchId, int workshopId);
}

public interface IWorkService
{
    Task<WorkDto> SubmitWorkAsync(SubmitWorkDto dto, int staffId);
    Task<WorkDto> ApproveWorkAsync(int workId, int qcId);
    Task<WorkDto> RejectWorkAsync(RejectWorkDto dto, int qcId);
    Task<IReadOnlyList<WorkDto>> GetWorksByBatchAsync(int batchId);
}

public interface ITransferService
{
    Task<TransferRequestDto> CreateTransferRequestAsync(CreateTransferDto dto);
    Task<TransferRequestDto> ApproveTransferAsync(ApproveTransferDto dto, int leadId);
    Task<IReadOnlyList<TransferRequestDto>> GetPendingTransfersAsync();
}

public interface IMaterialDeliveryService
{
    Task<MaterialDeliveryDto> ScheduleDeliveryAsync(CreateMaterialDeliveryDto dto);
    Task<MaterialDeliveryDto> ConfirmDeliveryAsync(ConfirmMaterialDeliveryDto dto, int qcId);
}

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<UserDto> RegisterAsync(RegisterDto dto);
}
