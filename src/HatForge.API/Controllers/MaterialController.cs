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

    [HttpPost("schedule")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<MaterialDeliveryDto>>> Schedule([FromBody] CreateMaterialDeliveryDto dto)
        => Success(await _materialService.ScheduleDeliveryAsync(dto));

    [HttpPut("confirm")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<MaterialDeliveryDto>>> Confirm([FromBody] ConfirmMaterialDeliveryDto dto)
        => Success(await _materialService.ConfirmDeliveryAsync(dto, CurrentUserId));
}
