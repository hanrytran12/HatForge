using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace HatForge.Infrastructure.Services;

public class CloudinaryFileStorageService : IFileStorageService
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorageService(IOptions<CloudinaryOptions> options)
    {
        var opt = options.Value;
        var account = new Account(opt.CloudName, opt.ApiKey, opt.ApiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public async Task<string> SaveAsync(Stream fileStream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ValidationException($"File type {ext} not allowed");

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "hatforge/works",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }
}
