namespace HatForge.Domain.Entities;

public class HatModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
}
