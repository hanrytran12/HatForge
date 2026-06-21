using HatForge.Application.Interfaces;
using HatForge.Domain.Interfaces;
using HatForge.Infrastructure.Data;
using HatForge.Infrastructure.Repositories;
using HatForge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HatForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INotificationStore, NotificationStore>();

        return services;
    }
}
