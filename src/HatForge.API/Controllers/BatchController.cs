using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
public class BatchController : BaseApiController
{
    private readonly IBatchService _batchService;

    public BatchController(IBatchService batchService) => _batchService = batchService;

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<BatchDto>>> Create([FromBody] CreateBatchDto dto)
        => Success(await _batchService.CreateBatchAsync(dto));

    [HttpPut("{id:int}/plan")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<BatchDto>>> Plan(int id, [FromBody] PlanBatchDto dto)
        => Success(await _batchService.PlanBatchAsync(id, dto, CurrentUserId));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BatchListDto>>>> GetAll()
        => Success(await _batchService.GetAllBatchesAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<BatchDto>>> GetById(int id)
    {
        var batch = await _batchService.GetBatchByIdAsync(id);
        if (batch == null)
            return NotFound(ApiResponse<BatchDto>.Fail($"Batch {id} not found"));
        return Success(batch);
    }

    [HttpPut("{id:int}/workshops/{workshopId:int}/complete")]
    [Authorize(Roles = nameof(UserRole.QCGate) + "," + nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<BatchDto>>> CompleteWorkshop(int id, int workshopId)
        => Success(await _batchService.MarkWorkshopCompletedAsync(id, workshopId));
}
