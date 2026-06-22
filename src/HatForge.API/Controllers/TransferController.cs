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
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<CreateTransferResultDto>>> Create([FromBody] CreateTransferDto dto)
        => Success(await _transferService.CreateTransferRequestAsync(dto, CurrentUserId));

    [HttpPut("approve")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<TransferRequestDto>>> Approve([FromBody] ApproveTransferDto dto)
        => Success(await _transferService.ApproveTransferAsync(dto, CurrentUserId));

    [HttpPut("confirm-receipt")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<TransferRequestDto>>> ConfirmReceipt([FromBody] ConfirmReceiptDto dto)
        => Success(await _transferService.ConfirmReceiptAsync(dto, CurrentUserId));

    [HttpGet("pending")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransferRequestDto>>>> GetPending()
        => Success(await _transferService.GetPendingTransfersAsync());

    [HttpGet("awaiting-receipt")]
    [Authorize(Roles = nameof(UserRole.QCWorkshop))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransferRequestDto>>>> GetAwaitingReceipt()
    {
        var workshopId = CurrentWorkshopId;
        if (workshopId == null)
            return BadRequest(ApiResponse<IReadOnlyList<TransferRequestDto>>.Fail("You are not assigned to any workshop"));

        return Success(await _transferService.GetAwaitingReceiptByWorkshopAsync(workshopId.Value));
    }
}
