using HatForge.Domain.Enums;

namespace HatForge.Application.DTOs;

public record CreateLeadTaskDelegationDto(
    LeadTaskDelegationType Type,
    int TaskId,
    int AssignedTransportQcId,
    string? Reason);

public record ReviewLeadTaskDelegationDto(string? AdminNotes);

public record LeadTaskDelegationDto(
    int Id,
    int BatchId,
    string BatchNumber,
    LeadTaskDelegationType Type,
    string TypeName,
    LeadTaskDelegationStatus Status,
    string StatusName,
    int? MaterialDeliveryId,
    int? TransferRequestId,
    int RequestedByLeadId,
    string RequestedByLeadName,
    int AssignedTransportQcId,
    string AssignedTransportQcName,
    int? ReviewedByAdminId,
    string? ReviewedByAdminName,
    int? CompletedByTransportQcId,
    string? CompletedByTransportQcName,
    string? Reason,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    DateTime? CompletedAt);
