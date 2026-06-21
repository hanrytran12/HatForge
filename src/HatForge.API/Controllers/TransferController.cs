using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
public class TransferController : BaseApiController
{
    private readonly ITransferService _transferService;

    public TransferController(ITransferService transferService) => _transferService = transferService;

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<TransferRequestDto>>> Create([FromBody] CreateTransferDto dto)
        => Success(await _transferService.CreateTransferRequestAsync(dto));

    [HttpPut("approve")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<TransferRequestDto>>> Approve([FromBody] ApproveTransferDto dto)
        => Success(await _transferService.ApproveTransferAsync(dto, CurrentUserId));

    [HttpGet("pending")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransferRequestDto>>>> GetPending()
        => Success(await _transferService.GetPendingTransfersAsync());
}
