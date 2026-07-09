using HatForge.Domain.Entities;

namespace HatForge.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<Batch> Batches { get; }
    IRepository<Work> Works { get; }
    IRepository<WorkPhoto> WorkPhotos { get; }
    IRepository<TransferRequest> TransferRequests { get; }
    IRepository<MaterialDelivery> MaterialDeliveries { get; }
    IRepository<MaterialDeliveryItem> MaterialDeliveryItems { get; }
    IRepository<MaterialRequest> MaterialRequests { get; }
    IRepository<MaterialRequestItem> MaterialRequestItems { get; }
    IRepository<LeadMaterialStock> LeadMaterialStocks { get; }
    IRepository<LeadMaterialStockTransaction> LeadMaterialStockTransactions { get; }
    IRepository<LeadTaskDelegationRequest> LeadTaskDelegationRequests { get; }
    IRepository<BatchWorkshop> BatchWorkshops { get; }
    IRepository<Workshop> Workshops { get; }
    IRepository<HatModel> HatModels { get; }
    IRepository<User> Users { get; }
    IRepository<Notification> Notifications { get; }

    Task<int> SaveChangesAsync();
    Task ExecuteInTransactionAsync(Func<Task> action);
}
