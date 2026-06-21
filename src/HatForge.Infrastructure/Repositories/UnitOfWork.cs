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
    private Repository<TransferRequest>? _transferRequests;
    private Repository<MaterialDelivery>? _materialDeliveries;
    private Repository<BatchWorkshop>? _batchWorkshops;
    private Repository<Workshop>? _workshops;
    private Repository<HatModel>? _hatModels;
    private Repository<User>? _users;
    private Repository<Notification>? _notifications;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IRepository<Batch> Batches => _batches ??= new Repository<Batch>(_context);
    public IRepository<Work> Works => _works ??= new Repository<Work>(_context);
    public IRepository<TransferRequest> TransferRequests => _transferRequests ??= new Repository<TransferRequest>(_context);
    public IRepository<MaterialDelivery> MaterialDeliveries => _materialDeliveries ??= new Repository<MaterialDelivery>(_context);
    public IRepository<BatchWorkshop> BatchWorkshops => _batchWorkshops ??= new Repository<BatchWorkshop>(_context);
    public IRepository<Workshop> Workshops => _workshops ??= new Repository<Workshop>(_context);
    public IRepository<HatModel> HatModels => _hatModels ??= new Repository<HatModel>(_context);
    public IRepository<User> Users => _users ??= new Repository<User>(_context);
    public IRepository<Notification> Notifications => _notifications ??= new Repository<Notification>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
