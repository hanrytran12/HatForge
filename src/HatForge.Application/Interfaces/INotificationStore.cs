namespace HatForge.Application.Interfaces;

public interface INotificationStore
{
    Task SaveAsync(int userId, string type, string title, string message, object? payload = null);
}
