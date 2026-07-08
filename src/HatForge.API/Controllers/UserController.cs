using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public class UserController : BaseApiController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService) => _userService = userService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> GetAll()
        => Success(await _userService.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] RegisterDto dto)
        => Success(await _userService.CreateAsync(dto));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteStaff(int id)
    {
        await _userService.DeleteStaffAsync(id);
        return Success(new { });
    }
}
