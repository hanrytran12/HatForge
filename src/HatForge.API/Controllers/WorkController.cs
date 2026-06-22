using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
public class WorkController : BaseApiController
{
    private readonly IWorkService _workService;
    private readonly IFileStorageService _fileStorage;

    public WorkController(IWorkService workService, IFileStorageService fileStorage)
    {
        _workService = workService;
        _fileStorage = fileStorage;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Staff))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<WorkDto>>> Submit(
        [FromForm] int batchId,
        [FromForm] int workshopId,
        [FromForm] int quantity,
        IFormFileCollection photos)
    {
        if (photos == null || photos.Count == 0)
            return BadRequest(ApiResponse<WorkDto>.Fail("At least one photo is required"));

        var photoUrls = new List<string>();
        foreach (var photo in photos)
        {
            await using var stream = photo.OpenReadStream();
            photoUrls.Add(await _fileStorage.SaveAsync(stream, photo.FileName, photo.ContentType));
        }

        var dto = new SubmitWorkDto(batchId, workshopId, quantity, photoUrls);
        return Success(await _workService.SubmitWorkAsync(dto, CurrentUserId));
    }

    [HttpPut("approve")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<WorkDto>>> Approve([FromBody] ApproveWorkDto dto)
        => Success(await _workService.ApproveWorkAsync(dto.WorkId, CurrentUserId));

    [HttpPut("reject")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<WorkDto>>> Reject(
        [FromForm] int workId,
        [FromForm] string rejectionNotes,
        IFormFileCollection photos)
    {
        var photoUrls = new List<string>();
        if (photos != null)
        {
            foreach (var photo in photos)
            {
                await using var stream = photo.OpenReadStream();
                photoUrls.Add(await _fileStorage.SaveAsync(stream, photo.FileName, photo.ContentType));
            }
        }

        var dto = new RejectWorkDto(workId, rejectionNotes, photoUrls);
        return Success(await _workService.RejectWorkAsync(dto, CurrentUserId));
    }

    [HttpGet("batch/{batchId:int}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkDto>>>> GetByBatch(int batchId)
        => Success(await _workService.GetWorksByBatchAsync(batchId));

    [HttpGet("batch/{batchId:int}/workshop/{workshopId:int}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkDto>>>> GetByBatchAndWorkshop(int batchId, int workshopId)
        => Success(await _workService.GetWorksByBatchAndWorkshopAsync(batchId, workshopId));
}
