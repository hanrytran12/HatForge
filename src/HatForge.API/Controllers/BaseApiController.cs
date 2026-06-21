using System.Security.Claims;
using HatForge.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedException("User id claim missing");

    protected OkObjectResult Success<T>(T data) =>
        Ok(ApiResponse<T>.Ok(data));
}
