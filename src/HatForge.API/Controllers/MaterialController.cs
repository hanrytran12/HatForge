using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
public class MaterialController : BaseApiController
{
    private readonly IMaterialDeliveryService _materialService;

    public MaterialController(IMaterialDeliveryService materialService) => _materialService = materialService;

    [HttpGet("pending")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MaterialDeliveryDto>>>> GetPending()
    {
        var workshopId = CurrentWorkshopId;
        if (workshopId == null)
            return BadRequest(ApiResponse<IReadOnlyList<MaterialDeliveryDto>>.Fail("You are not assigned to any workshop"));

        return Success(await _materialService.GetPendingByWorkshopAsync(workshopId.Value));
    }

    [HttpPut("confirm")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<MaterialDeliveryDto>>> Confirm([FromBody] ConfirmMaterialDeliveryDto dto)
        => Success(await _materialService.ConfirmDeliveryAsync(dto, CurrentUserId));
}
