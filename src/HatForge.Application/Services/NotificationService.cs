using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(int userId)
    {
        var notifications = await _unitOfWork.Notifications.FindAsync(
            x => x.UserId == userId);

        return notifications
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationDto(
                x.Id, x.Type, x.Title, x.Message,
                x.Payload, x.IsRead, x.CreatedAt))
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var unread = await _unitOfWork.Notifications.FindAsync(
            x => x.UserId == userId && !x.IsRead);
        return unread.Count;
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _unitOfWork.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId)
            ?? throw new NotFoundException("Notification not found");

        if (notification.IsRead) return;

        notification.IsRead = true;
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _unitOfWork.Notifications.FindAsync(
            x => x.UserId == userId && !x.IsRead);

        foreach (var n in unread)
        {
            n.IsRead = true;
            _unitOfWork.Notifications.Update(n);
        }

        if (unread.Count > 0)
            await _unitOfWork.SaveChangesAsync();
    }
}
