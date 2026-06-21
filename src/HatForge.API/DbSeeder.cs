using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HatForge.API;

public class DbSeederHostedService : IHostedService
{
    private readonly IServiceProvider _services;

    public DbSeederHostedService(IServiceProvider services) => _services = services;

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await ctx.Database.CanConnectAsync(ct) == false) return;
        await ctx.Database.MigrateAsync();
        if (await ctx.Users.AnyAsync()) return;

        string Pw(string p) => hasher.Hash(p);
        ctx.Workshops.AddRange(
            new Workshop { Name = "Cutting", RequiresMaterials = true },
            new Workshop { Name = "Sewing", RequiresMaterials = false },
            new Workshop { Name = "Finishing", RequiresMaterials = false }
        );
        await ctx.SaveChangesAsync();

        ctx.HatModels.Add(new HatModel { Name = "Classic Fedora", Description = "Wool felt fedora" });
        await ctx.SaveChangesAsync();

        ctx.Users.AddRange(
            new User { Email = "admin@hatforge.com", Name = "Admin", Role = UserRole.Admin, PasswordHash = Pw("Admin123!") },
            new User { Email = "lead@hatforge.com", Name = "Lead", Role = UserRole.Lead, PasswordHash = Pw("Lead123!") },
            new User { Email = "staff@hatforge.com", Name = "Staff", Role = UserRole.Staff, WorkshopId = 1, PasswordHash = Pw("Staff123!") },
            new User { Email = "qc@hatforge.com", Name = "QC Workshop", Role = UserRole.QCWorkshop, WorkshopId = 1, PasswordHash = Pw("Qc123!") },
            new User { Email = "gate@hatforge.com", Name = "QC Gate", Role = UserRole.QCGate, PasswordHash = Pw("Gate123!") }
        );
        await ctx.SaveChangesAsync();
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
