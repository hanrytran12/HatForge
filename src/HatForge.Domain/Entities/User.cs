using HatForge.Domain.Enums;

namespace HatForge.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? WorkshopId { get; set; }
    public bool IsActive { get; set; } = true;

    public Workshop? Workshop { get; set; }
}
