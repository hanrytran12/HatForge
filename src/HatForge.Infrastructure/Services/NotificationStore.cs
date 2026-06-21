using System.Text.Json;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Interfaces;

namespace HatForge.Infrastructure.Services;

public class NotificationStore : INotificationStore
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationStore(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task SaveAsync(int userId, string type, string title, string message, object? payload = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Payload = payload != null
                ? JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : null
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();
    }
}
