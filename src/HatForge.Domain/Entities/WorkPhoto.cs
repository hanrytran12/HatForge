namespace HatForge.Domain.Entities;

public enum WorkPhotoType { Submission, Rejection }

public class WorkPhoto
{
    public int Id { get; set; }
    public int WorkId { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public WorkPhotoType Type { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Work Work { get; set; } = null!;
}
