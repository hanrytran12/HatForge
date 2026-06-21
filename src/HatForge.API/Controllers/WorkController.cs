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

    public WorkController(IWorkService workService) => _workService = workService;

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Staff))]
    public async Task<ActionResult<WorkDto>> Submit([FromBody] SubmitWorkDto dto)
        => Ok(await _workService.SubmitWorkAsync(dto, CurrentUserId));

    [HttpPut("approve")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<WorkDto>> Approve([FromBody] ApproveWorkDto dto)
        => Ok(await _workService.ApproveWorkAsync(dto.WorkId, CurrentUserId));

    [HttpPut("reject")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<WorkDto>> Reject([FromBody] RejectWorkDto dto)
        => Ok(await _workService.RejectWorkAsync(dto, CurrentUserId));

    [HttpGet("batch/{batchId:int}")]
    public async Task<ActionResult<IReadOnlyList<WorkDto>>> GetByBatch(int batchId)
        => Ok(await _workService.GetWorksByBatchAsync(batchId));
}
