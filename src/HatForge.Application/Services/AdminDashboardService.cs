using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private const int LatestPendingDelegationLimit = 5;

    private readonly IUnitOfWork _unitOfWork;

    public AdminDashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminDashboardDto> GetAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var batches = await _unitOfWork.Batches.FindAsync(x => true);
        var pendingDelegations = await _unitOfWork.LeadTaskDelegationRequests.FindAsync(
            x => x.Status == LeadTaskDelegationStatus.PendingAdminApproval,
            new[] { "Batch", "RequestedByLead", "AssignedTransportQc" });
        var activeUsers = await _unitOfWork.Users.FindAsync(x => x.IsActive, new[] { "Workshop" });
        var activeStaff = activeUsers.Where(x => x.Role == UserRole.Staff).ToList();

        var batchStatusCounts = Enum.GetValues<BatchStatus>()
            .Select(status => new BatchStatusCountDto(
                status.ToString(),
                batches.Count(x => x.Status == status)))
            .ToList();

        var activeBatches = batches.Where(IsActiveBatch).ToList();
        var completedThisMonth = batches.Count(x =>
            x.Status == BatchStatus.Completed
            && x.CompletedAt.HasValue
            && x.CompletedAt.Value.Year == now.Year
            && x.CompletedAt.Value.Month == now.Month);

        var kpis = new AdminDashboardKpiDto(
            activeBatches.Count,
            activeBatches.Count(x => x.EndDate.Date < today),
            completedThisMonth,
            pendingDelegations.Count,
            activeUsers.Count,
            activeStaff.Count);

        var latestPendingDelegations = pendingDelegations
            .OrderByDescending(x => x.CreatedAt)
            .Take(LatestPendingDelegationLimit)
            .Select(MapPendingDelegation)
            .ToList();

        var userSummary = await BuildUserSummaryAsync(activeUsers, activeStaff);

        return new AdminDashboardDto(
            now,
            kpis,
            batchStatusCounts,
            latestPendingDelegations,
            userSummary);
    }

    private async Task<AdminDashboardUserSummaryDto> BuildUserSummaryAsync(
        IReadOnlyList<Domain.Entities.User> activeUsers,
        IReadOnlyList<Domain.Entities.User> activeStaff)
    {
        var activeWorks = await _unitOfWork.Works.FindAsync(
            x => true,
            new[] { "Batch" });
        var activeWorksByStaff = activeWorks
            .Where(x => x.Batch != null && IsActiveBatch(x.Batch))
            .GroupBy(x => x.StaffId)
            .ToDictionary(x => x.Key, x => x.Count());

        var roleCounts = Enum.GetValues<UserRole>()
            .Select(role => new UserRoleCountDto(
                role.ToString(),
                activeUsers.Count(x => x.Role == role)))
            .ToList();

        var staffByWorkshop = activeStaff
            .GroupBy(x => new { x.WorkshopId, WorkshopName = x.Workshop?.Name ?? "Unassigned" })
            .OrderBy(x => x.Key.WorkshopName)
            .Select(x => new StaffByWorkshopDto(
                x.Key.WorkshopId,
                x.Key.WorkshopName,
                x.Count()))
            .ToList();

        var staffWorkStatuses = activeStaff
            .OrderBy(x => x.Workshop?.Name ?? "")
            .ThenBy(x => x.Name)
            .Select(staff =>
            {
                var activeWorkCount = activeWorksByStaff.TryGetValue(staff.Id, out var count)
                    ? count
                    : 0;
                return new StaffWorkStatusDto(
                    staff.Id,
                    staff.Name,
                    staff.WorkshopId,
                    staff.Workshop?.Name ?? "",
                    activeWorkCount > 0,
                    activeWorkCount);
            })
            .ToList();

        return new AdminDashboardUserSummaryDto(
            roleCounts,
            staffByWorkshop,
            staffWorkStatuses);
    }

    private static PendingAdminDelegationSummaryDto MapPendingDelegation(LeadTaskDelegationRequest request) => new(
        request.Id,
        request.BatchId,
        request.Batch?.BatchNumber ?? "",
        request.Type.ToString(),
        request.MaterialDeliveryId,
        request.TransferRequestId,
        request.MaterialRequestId,
        request.RequestedByLeadId,
        request.RequestedByLead?.Name ?? "",
        request.AssignedTransportQcId,
        request.AssignedTransportQc?.Name ?? "",
        request.Reason,
        request.CreatedAt);

    private static bool IsActiveBatch(Batch batch) =>
        batch.Status is not (BatchStatus.Created or BatchStatus.Completed);
}
