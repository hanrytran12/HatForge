namespace HatForge.Application.DTOs;

public record AdminDashboardDto(
    DateTime GeneratedAt,
    AdminDashboardKpiDto Kpis,
    IReadOnlyList<BatchStatusCountDto> BatchStatusCounts,
    IReadOnlyList<PendingAdminDelegationSummaryDto> LatestPendingDelegations,
    AdminDashboardUserSummaryDto UserSummary
);

public record AdminDashboardKpiDto(
    int ActiveBatches,
    int OverdueBatches,
    int CompletedThisMonth,
    int PendingAdminDelegations,
    int ActiveUsers,
    int ActiveStaff
);

public record BatchStatusCountDto(
    string Status,
    int Count
);

public record PendingAdminDelegationSummaryDto(
    int Id,
    int BatchId,
    string BatchNumber,
    string Type,
    int? MaterialDeliveryId,
    int? TransferRequestId,
    int? MaterialRequestId,
    int RequestedByLeadId,
    string RequestedByLeadName,
    int AssignedTransportQcId,
    string AssignedTransportQcName,
    string? Reason,
    DateTime CreatedAt
);

public record AdminDashboardUserSummaryDto(
    IReadOnlyList<UserRoleCountDto> RoleCounts,
    IReadOnlyList<StaffByWorkshopDto> StaffByWorkshop,
    IReadOnlyList<StaffWorkStatusDto> StaffWorkStatuses
);

public record UserRoleCountDto(
    string Role,
    int Count
);

public record StaffByWorkshopDto(
    int? WorkshopId,
    string WorkshopName,
    int ActiveStaffCount
);

public record StaffWorkStatusDto(
    int StaffId,
    string StaffName,
    int? WorkshopId,
    string WorkshopName,
    bool HasActiveWork,
    int ActiveWorkCount
);
