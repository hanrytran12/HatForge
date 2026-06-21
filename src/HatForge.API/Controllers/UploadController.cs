using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IFileStorageService _fileStorage;

    public UploadController(IFileStorageService fileStorage) => _fileStorage = fileStorage;

    [HttpPost("photo")]
    public async Task<ActionResult<ApiResponse<object>>> UploadPhoto(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file provided"));

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveAsync(stream, file.FileName, file.ContentType);
        return Ok(ApiResponse<object>.Ok(new { url }));
    }
}
