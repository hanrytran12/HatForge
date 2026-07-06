using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize]
[Route("api/lead-task-delegation")]
public class LeadTaskDelegationController : BaseApiController
{
    private readonly ILeadTaskDelegationService _delegationService;

    public LeadTaskDelegationController(ILeadTaskDelegationService delegationService)
        => _delegationService = delegationService;

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> Create(
        [FromBody] CreateLeadTaskDelegationDto dto)
        => Success(await _delegationService.CreateAsync(dto, CurrentUserId));

    [HttpGet("my-requests")]
    [Authorize(Roles = nameof(UserRole.Lead))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeadTaskDelegationDto>>>> GetMyRequests()
        => Success(await _delegationService.GetRequestedByLeadAsync(CurrentUserId));

    [HttpGet("pending-admin")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeadTaskDelegationDto>>>> GetPendingForAdmin()
        => Success(await _delegationService.GetPendingForAdminAsync(CurrentUserId));

    [HttpGet("my-assignments")]
    [Authorize(Roles = nameof(UserRole.QCTransport))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeadTaskDelegationDto>>>> GetMyAssignments()
        => Success(await _delegationService.GetAssignedToTransportQcAsync(CurrentUserId));

    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> Approve(
        int id,
        [FromBody] ReviewLeadTaskDelegationDto dto)
        => Success(await _delegationService.ApproveAsync(id, CurrentUserId, dto));

    [HttpPut("{id:int}/reject")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> Reject(
        int id,
        [FromBody] ReviewLeadTaskDelegationDto dto)
        => Success(await _delegationService.RejectAsync(id, CurrentUserId, dto));

    [HttpPut("{id:int}/material-delivered")]
    [Authorize(Roles = nameof(UserRole.QCTransport))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> MarkMaterialDelivered(int id)
        => Success(await _delegationService.MarkMaterialDeliveredAsync(id, CurrentUserId));

    [HttpPut("{id:int}/approve-transfer")]
    [Authorize(Roles = nameof(UserRole.QCTransport))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> ApproveDelegatedTransfer(int id)
        => Success(await _delegationService.ApproveDelegatedTransferAsync(id, CurrentUserId));

    [HttpPut("{id:int}/approve-final-review")]
    [Authorize(Roles = nameof(UserRole.QCTransport))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> ApproveDelegatedFinalReview(int id)
        => Success(await _delegationService.ApproveDelegatedFinalReviewAsync(id, CurrentUserId));

    [HttpPut("{id:int}/material-request-delivered")]
    [Authorize(Roles = nameof(UserRole.QCTransport))]
    public async Task<ActionResult<ApiResponse<LeadTaskDelegationDto>>> MarkMaterialRequestDelivered(int id)
        => Success(await _delegationService.MarkMaterialRequestDeliveredAsync(id, CurrentUserId));
}
