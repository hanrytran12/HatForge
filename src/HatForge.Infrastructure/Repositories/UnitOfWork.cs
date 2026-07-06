using HatForge.Domain.Entities;
using HatForge.Domain.Interfaces;
using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HatForge.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private Repository<Batch>? _batches;
    private Repository<Work>? _works;
    private Repository<WorkPhoto>? _workPhotos;
    private Repository<TransferRequest>? _transferRequests;
    private Repository<MaterialDelivery>? _materialDeliveries;
    private Repository<MaterialDeliveryItem>? _materialDeliveryItems;
    private Repository<MaterialRequest>? _materialRequests;
    private Repository<MaterialRequestItem>? _materialRequestItems;
    private Repository<LeadTaskDelegationRequest>? _leadTaskDelegationRequests;
    private Repository<BatchWorkshop>? _batchWorkshops;
    private Repository<Workshop>? _workshops;
    private Repository<HatModel>? _hatModels;
    private Repository<User>? _users;
    private Repository<Notification>? _notifications;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IRepository<Batch> Batches => _batches ??= new Repository<Batch>(_context);
    public IRepository<Work> Works => _works ??= new Repository<Work>(_context);
    public IRepository<WorkPhoto> WorkPhotos => _workPhotos ??= new Repository<WorkPhoto>(_context);
    public IRepository<TransferRequest> TransferRequests => _transferRequests ??= new Repository<TransferRequest>(_context);
    public IRepository<MaterialDelivery> MaterialDeliveries => _materialDeliveries ??= new Repository<MaterialDelivery>(_context);
    public IRepository<MaterialDeliveryItem> MaterialDeliveryItems => _materialDeliveryItems ??= new Repository<MaterialDeliveryItem>(_context);
    public IRepository<MaterialRequest> MaterialRequests => _materialRequests ??= new Repository<MaterialRequest>(_context);
    public IRepository<MaterialRequestItem> MaterialRequestItems => _materialRequestItems ??= new Repository<MaterialRequestItem>(_context);
    public IRepository<LeadTaskDelegationRequest> LeadTaskDelegationRequests => _leadTaskDelegationRequests ??= new Repository<LeadTaskDelegationRequest>(_context);
    public IRepository<BatchWorkshop> BatchWorkshops => _batchWorkshops ??= new Repository<BatchWorkshop>(_context);
    public IRepository<Workshop> Workshops => _workshops ??= new Repository<Workshop>(_context);
    public IRepository<HatModel> HatModels => _hatModels ??= new Repository<HatModel>(_context);
    public IRepository<User> Users => _users ??= new Repository<User>(_context);
    public IRepository<Notification> Notifications => _notifications ??= new Repository<Notification>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
