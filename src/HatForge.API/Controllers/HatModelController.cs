using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
public class HatModelController : BaseApiController
{
    private readonly IHatModelService _hatModelService;

    public HatModelController(IHatModelService hatModelService) => _hatModelService = hatModelService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HatModelDto>>>> GetAll()
        => Success(await _hatModelService.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<HatModelDto>>> Create([FromBody] CreateHatModelDto dto)
        => Success(await _hatModelService.CreateAsync(dto));

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<HatModelDto>>> Update(int id, [FromBody] UpdateHatModelDto dto)
        => Success(await _hatModelService.UpdateAsync(id, dto));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _hatModelService.DeleteAsync(id);
        return Success(new { });
    }
}
