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
    public async Task<ActionResult<BatchDto>> Create([FromBody] CreateBatchDto dto)
        => Ok(await _batchService.CreateBatchAsync(dto));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BatchListDto>>> GetAll()
        => Ok(await _batchService.GetAllBatchesAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BatchDto>> GetById(int id)
    {
        var batch = await _batchService.GetBatchByIdAsync(id);
        return batch == null ? NotFound() : Ok(batch);
    }

    [HttpPut("{id:int}/assign-lead")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<BatchDto>> AssignLead(int id, [FromBody] AssignLeadDto dto)
        => Ok(await _batchService.AssignLeadAsync(id, dto.LeadId));

    [HttpPut("{id:int}/workshops/{workshopId:int}/complete")]
    [Authorize(Roles = nameof(UserRole.QCGate) + "," + nameof(UserRole.Lead))]
    public async Task<ActionResult<BatchDto>> CompleteWorkshop(int id, int workshopId)
        => Ok(await _batchService.MarkWorkshopCompletedAsync(id, workshopId));
}
