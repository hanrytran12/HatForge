using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Infrastructure.Data;
using HatForge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HatForge.Tests.Fixtures;

public static class TestDataFactory
{
    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    public static UnitOfWork CreateUnitOfWork(AppDbContext context) => new(context);

    public static User Lead(int id = 1) => new()
        { Id = id, Email = $"lead{id}@hf.com", Name = "Lead", Role = UserRole.Lead, PasswordHash = "x" };

    public static User Staff(int id = 2, int? workshopId = 1) => new()
        { Id = id, Email = $"staff{id}@hf.com", Name = "Staff", Role = UserRole.Staff, WorkshopId = workshopId, PasswordHash = "x" };

    public static User QcWorkshop(int id = 3, int? workshopId = 1) => new()
        { Id = id, Email = $"qc{id}@hf.com", Name = "QC", Role = UserRole.QCWorkshop, WorkshopId = workshopId, PasswordHash = "x" };

    public static User Admin(int id = 4) => new()
        { Id = id, Email = $"admin{id}@hf.com", Name = "Admin", Role = UserRole.Admin, PasswordHash = "x" };

    public static User QcGate(int id = 6) => new()
        { Id = id, Email = $"qcgate{id}@hf.com", Name = "QCGate", Role = UserRole.QCGate, PasswordHash = "x" };

    public static Workshop Workshop(int id = 1, bool requiresMaterials = false) => new()
        { Id = id, Name = $"Workshop {id}", RequiresMaterials = requiresMaterials };

    public static HatModel HatModel(int id = 1) => new()
        { Id = id, Name = "Fedora" };

    public static async Task SeedBaseAsync(AppDbContext ctx)
    {
        ctx.Users.AddRange(Lead(), Staff(), QcWorkshop(), Admin());
        ctx.Workshops.AddRange(
            Workshop(1, requiresMaterials: false),
            Workshop(2, requiresMaterials: false),
            Workshop(3, requiresMaterials: true));
        ctx.HatModels.Add(HatModel());
        await ctx.SaveChangesAsync();
    }
}
