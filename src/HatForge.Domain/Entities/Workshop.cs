namespace HatForge.Domain.Entities;

public class Workshop
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool RequiresMaterials { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<BatchWorkshop> BatchWorkshops { get; set; } = new List<BatchWorkshop>();
}
