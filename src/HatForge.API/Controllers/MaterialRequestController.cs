using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
public class MaterialRequestController : BaseApiController
{
    private readonly IMaterialRequestService _materialRequestService;

    public MaterialRequestController(IMaterialRequestService materialRequestService)
        => _materialRequestService = materialRequestService;

    [HttpGet("pending")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MaterialRequestDto>>>> GetPending()
        => Success(await _materialRequestService.GetPendingForLeadAsync(CurrentUserId));

    [HttpGet("batch/{batchId:int}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MaterialRequestDto>>>> GetByBatch(int batchId)
        => Success(await _materialRequestService.GetByBatchAsync(batchId));

    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<MaterialRequestDto>>> Approve(int id)
        => Success(await _materialRequestService.ApproveAsync(id, CurrentUserId));

    [HttpPut("{id:int}/confirm")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<MaterialRequestDto>>> Confirm(
        int id, [FromBody] ConfirmMaterialRequestDto dto)
    {
        if (dto.RequestId != id)
            return BadRequest(ApiResponse<MaterialRequestDto>.Fail("Route id and requestId mismatch"));
        return Success(await _materialRequestService.ConfirmAsync(dto, CurrentUserId));
    }
}