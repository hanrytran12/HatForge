using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace HatForge.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly string _uploadRoot;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _uploadRoot = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(_uploadRoot);
    }

    public async Task<string> SaveAsync(Stream fileStream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ValidationException($"File type {ext} not allowed");

        var safeName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_uploadRoot, safeName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(stream);

        return $"/uploads/{safeName}";
    }
}
