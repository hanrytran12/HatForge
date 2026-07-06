# Data Models

## Entity Relationship Overview

```
HatModel (1) ──────────────── (*) Batch
User [Lead] (1) ────────────── (*) Batch.AssignedToLead

Batch (1) ──────────────────── (*) BatchWorkshop ── (1) Workshop
Batch (1) ──────────────────── (*) Work
Batch (1) ──────────────────── (*) TransferRequest
Batch (1) ──────────────────── (*) MaterialDelivery
Batch (1) ──────────────────── (*) MaterialRequest
Batch (1) ──────────────────── (*) LeadTaskDelegationRequest

Workshop (1) ───────────────── (*) User
Workshop (1) ───────────────── (*) BatchWorkshop

Work (*) ── (1) Batch
Work (*) ── (1) Workshop
Work (*) ── (1) User [Staff]
Work (*) ── (0..1) User [ReviewedByQC]
Work (1) ── (*) WorkPhoto

TransferRequest (*) ── (1) Batch
TransferRequest (*) ── (1) Workshop [From]
TransferRequest (*) ── (1) Workshop [To]
TransferRequest (*) ── (0..1) User [CreatedByQC]
TransferRequest (*) ── (0..1) User [ApprovedByLead]
TransferRequest (*) ── (0..1) User [ConfirmedByQC]

MaterialDelivery (1) ── (*) MaterialDeliveryItem
MaterialDelivery (*) ── (1) Batch
MaterialDelivery (*) ── (1) Workshop
MaterialDelivery (1) ── (*) MaterialRequest (via MaterialRequest.OriginalDeliveryId)

MaterialRequest (1) ── (*) MaterialRequestItem
MaterialRequest (*) ── (1) Batch
MaterialRequest (*) ── (1) Workshop
MaterialRequest (*) ── (0..1) MaterialDelivery [OriginalDelivery]
MaterialRequest (*) ── (1) User [CreatedByQC]
MaterialRequest (*) ── (0..1) User [ApprovedByLead]
MaterialRequest (*) ── (0..1) User [FulfilledByQC]

LeadTaskDelegationRequest (*) ── (1) Batch
LeadTaskDelegationRequest (*) ── (0..1) MaterialDelivery
LeadTaskDelegationRequest (*) ── (0..1) TransferRequest
LeadTaskDelegationRequest (*) ── (1) User [RequestedByLead]
LeadTaskDelegationRequest (*) ── (1) User [AssignedTransportQc]
LeadTaskDelegationRequest (*) ── (0..1) User [ReviewedByAdmin]
LeadTaskDelegationRequest (*) ── (0..1) User [CompletedByTransportQc]

Notification (*) ── (1) User
```

---

## Entities

### Batch
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchNumber | string | Unique, max 64, format `BATCH-YYYYMMDD-XXXX` |
| HatModelId | int | FK → HatModel |
| AssignedLeadId | int? | FK → User (Lead) |
| Status | BatchStatus | Persisted as int — do not renumber enum |
| TargetQuantity | int | |
| StartDate | DateTime | Non-nullable |
| EndDate | DateTime | Non-nullable |
| CreatedAt | DateTime | |
| CompletedAt | DateTime? | Set on Gate QC confirm |
| CompletedQuantity | int? | Auto-computed on Gate confirm = sum of `Work.PassedQuantity` at last workshop |
| BatchWorkshops | ICollection\<BatchWorkshop\> | Nav |
| Works | ICollection\<Work\> | Nav |
| TransferRequests | ICollection\<TransferRequest\> | Nav |

### BatchWorkshop (join + state per workshop)
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| OrderIndex | int | Server normalizes to 0-based sequential during planning |
| RequiresMaterials | bool | If true, Staff cannot submit work until `MaterialsReceived = true` |
| MaterialsReceived | bool | Set true on material delivery confirmation |
| IsCompleted | bool | Marked true when source's transfer is confirmed (or last workshop completes) |
| StartDate | DateTime | Non-nullable |
| EndDate | DateTime | Non-nullable |
| InitialMaterialQty | decimal | Total delivered (initial delivery + fulfilled top-up requests); sum of `MaterialDeliveryItem.ActualQuantity` |
| MaterialUsed | decimal | Pre-charged on submit (estimated), reconciled to actual on approve/reject |
| EstimatedMetersPerUnit | decimal | Used for pre-charge calculation: `quantity * estimatedMetersPerUnit` |

Unique index: `(BatchId, WorkshopId)`

### Workshop
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | |
| RequiresMaterials | bool | Drives `BatchWorkshop.RequiresMaterials` default and ad-hoc request eligibility |
| Users | ICollection\<User\> | Nav |

### User
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | Max 128 |
| Email | string | Unique, max 256 |
| PasswordHash | string | BCrypt |
| Role | UserRole | Admin=0, Lead=1, Staff=2, QCWorkshop=3, QCGate=4, QCTransport=5 |
| WorkshopId | int? | Null for Admin, Lead, QCGate |
| Workshop | Workshop? | Nav |

### Work
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| StaffId | int | FK → User (Staff) |
| Quantity | int | Submitted quantity |
| IsRework | bool | True when resubmitting repairable items from a prior rejection |
| SubmittedDate | DateTime | Defaults to UtcNow |
| Status | WorkStatus | Submitted=0, Approved=1, Rejected=2 |
| RejectionNotes | string? | Max 500, required on rejection |
| PassedQuantity | int | On approve: equals `Quantity`. On reject: QC's count of items that passed |
| RepairableQuantity | int | On reject only: items that can be resubmitted. Triggers rework submissions |
| UnrepairableQuantity | int | On reject only: items that cannot be repaired. Surfaced on `TransferRequestDto.QualityIssues` |
| ReviewedByQCId | int? | FK → User (QCWorkshop) |
| ReviewedAt | DateTime? | |
| ActualMaterialUsed | decimal? | QC's measurement; replaces the pre-charged estimate on approve/reject |
| EstimatedMaterialUsed | decimal? | Pre-charged at submit time: `quantity * BatchWorkshop.EstimatedMetersPerUnit` (only when workshop requires materials) |
| Photos | ICollection\<WorkPhoto\> | Nav |

Constraint: on reject, `PassedQuantity + RepairableQuantity + UnrepairableQuantity` must equal `Quantity`.

### WorkPhoto
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| WorkId | int | FK → Work |
| PhotoUrl | string | Max 512, Cloudinary URL |
| Type | WorkPhotoType | Submission=0, Rejection=1 |
| UploadedAt | DateTime | Server-stamped |

### TransferRequest
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| FromWorkshopId | int | FK → Workshop |
| ToWorkshopId | int | FK → Workshop |
| CreatedByQCId | int? | FK → User |
| ApprovedByLeadId | int? | FK → User |
| ConfirmedByQCId | int? | FK → User |
| Status | TransferStatus | Pending=0, Approved=1, Transferred=2 |
| CreatedAt | DateTime | |
| ApprovedAt | DateTime? | |
| ConfirmedAt | DateTime? | |
| ReceivedUsableQuantity | int? | Set by destination QC on receipt. Caps downstream non-rework work quantity for the next workshop |
| ReceivedDefectiveQuantity | int? | Set by destination QC on receipt. Unusable items found during inspection |
| ReceiptInspectionNotes | string? | Max 500 |

`ApprovedQuantity` is **not** persisted — it is computed at read time as the sum of `Work.PassedQuantity` for the source workshop. Same value is returned in `TransferRequestDto` and is the value the destination QC must match on receipt (`usable + defective == approved`).

### MaterialDelivery
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| ScheduledDate | DateTime | |
| DeliveredDate | DateTime? | Set when QCTransport marks delivered, or on workshop QC confirmation if not set earlier |
| IsConfirmed | bool | |
| ConfirmedByQCId | int? | FK → User |
| ConfirmedAt | DateTime? | |
| Status | MaterialDeliveryStatus | Scheduled=0, Delivered=1, Received=2 |
| Items | ICollection\<MaterialDeliveryItem\> | Nav |
| MaterialRequests | ICollection\<MaterialRequest\> | Nav (only fulfilled supplemental requests) |

### MaterialDeliveryItem
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| MaterialDeliveryId | int | FK → MaterialDelivery |
| MaterialName | string | Max 256 |
| PlannedQuantity | int | |
| ActualQuantity | int | Filled when QC confirms; sum of `ActualQuantity` is written to `BatchWorkshop.InitialMaterialQty` |

### MaterialRequest
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| OriginalDeliveryId | int? | FK → MaterialDelivery. Null for ad-hoc requests |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| Status | MaterialRequestStatus | Pending=0, Approved=1, Fulfilled=2 |
| Round | int | Starts at 1; increments when a still-short fulfillment spawns the next request |
| IsAdHoc | bool | True for QC-initiated top-up requests (not shortfall-driven) |
| Reason | string? | Required for ad-hoc; null for shortfall requests |
| CreatedByQCId | int | FK → User (QC) |
| CreatedAt | DateTime | |
| ApprovedByLeadId | int? | FK → User (Lead) |
| ApprovedAt | DateTime? | |
| FulfilledByQCId | int? | FK → User (QC) |
| FulfilledAt | DateTime? | |
| OriginalDelivery | MaterialDelivery? | Nav |
| Items | ICollection\<MaterialRequestItem\> | Nav |

**Round limit:** max 3 supplemental rounds (so up to 4 round numbers total including the original shortfall request). A 4th `ConfirmAsync` that is still short throws `BusinessRuleException`. Ad-hoc requests also count against this cap once they go through the same approve/confirm cycle.

**Origin rules:**
- Shortfall requests are auto-created by `MaterialDeliveryService.ConfirmDeliveryAsync` only when the delivery's workshop is the **first** in the batch chain (`OrderIndex == 0`).
- Ad-hoc requests can be created only by the first workshop's QC.

### MaterialRequestItem
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| MaterialRequestId | int | FK → MaterialRequest |
| MaterialName | string | Max 256 |
| Unit | string | Max 32 |
| ShortfallQuantity | int | What was still missing at the time the item was created |
| ActualQuantity | int? | Filled when QC confirms receipt |

### LeadTaskDelegationRequest
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| MaterialDeliveryId | int? | FK → MaterialDelivery; required when `Type = MaterialDelivery` |
| TransferRequestId | int? | FK → TransferRequest; required when `Type = TransferApproval` |
| Type | LeadTaskDelegationType | MaterialDelivery=0, TransferApproval=1 |
| Status | LeadTaskDelegationStatus | PendingAdminApproval=0, Approved=1, Rejected=2, Completed=3 |
| RequestedByLeadId | int | FK → User (Lead who created the request) |
| AssignedTransportQcId | int | FK → User (QCTransport assigned to execute) |
| ReviewedByAdminId | int? | FK → User (Admin who approved/rejected) |
| CompletedByTransportQcId | int? | FK → User (QCTransport who executed) |
| Reason | string? | Max 500 |
| AdminNotes | string? | Max 500 |
| CreatedAt / ReviewedAt / CompletedAt | DateTime | Audit timestamps |

Constraint: exactly one target is set. `MaterialDeliveryId` is used only for material delivery delegations; `TransferRequestId` is used only for transfer approval delegations.

### HatModel
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | |
| Description | string? | |

### Notification
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| UserId | int | FK → User |
| Type | string | Event name (e.g. `WorkApproved`, `MaterialShortfall`) |
| Title | string | Max 256 |
| Message | string | Max 1024 |
| Payload | string? | JSON-serialized event payload |
| IsRead | bool | |
| CreatedAt | DateTime | |

Index: `(UserId, IsRead)` for query performance.

---

## Enums

### BatchStatus
```csharp
Created = 0, Assigned = 1, InProduction = 2, UnderQCReview = 3,
ReadyForTransfer = 4, Completed = 5, PendingLeadReview = 6, PendingGateQC = 7
```
⚠️ Values are persisted to the DB. Gap between 5 and 6 is intentional. Never renumber.

### WorkStatus
```csharp
Submitted = 0, Approved = 1, Rejected = 2
```

### WorkPhotoType
```csharp
Submission = 0, Rejection = 1
```
Lives inline in `WorkPhoto.cs`.

### TransferStatus
```csharp
Pending = 0, Approved = 1, Transferred = 2
```

### MaterialDeliveryStatus
```csharp
Scheduled = 0, Delivered = 1, Received = 2
```

### MaterialRequestStatus
```csharp
Pending = 0, Approved = 1, Fulfilled = 2
```

### UserRole
```csharp
Admin = 0, Lead = 1, Staff = 2, QCWorkshop = 3, QCGate = 4, QCTransport = 5
```

### LeadTaskDelegationType
```csharp
MaterialDelivery = 0, TransferApproval = 1
```

### LeadTaskDelegationStatus
```csharp
PendingAdminApproval = 0, Approved = 1, Rejected = 2, Completed = 3
```

### RejectionReasonType
```csharp
Material = 0, Craftsmanship = 1, Design = 2, Equipment = 3, Other = 4
```
Defined but not currently wired to the `Work` entity.

---

## EF Core Constraints Summary

| Entity | Constraint |
|---|---|
| Batch.BatchNumber | Unique index, max 64 chars |
| BatchWorkshop | Unique composite index (BatchId, WorkshopId) |
| User.Email | Unique index, max 256 chars |
| User.Name | Max 128 chars |
| Notification | Composite index (UserId, IsRead) |
| PhotoUrl | Max 512 chars |
| RejectionNotes | Max 500 chars |
| MaterialName | Max 256 chars |
| MaterialRequestItem.Unit | Max 32 chars |
| LeadTaskDelegationRequest.Reason / AdminNotes | Max 500 chars |
| Delete behavior | `Restrict` on Workshop/User FKs; `Cascade` on Batch child records; `SetNull` on optional reviewer/approver FKs |

---

## Material Tracking Constants

`Application/Common/MaterialTracking.cs`:

```csharp
LowMaterialThresholdMeters = 5m
```

When the post-reconciliation `BatchWorkshop.InitialMaterialQty - MaterialUsed` drops to or below this threshold, `WorkService` fires a `MaterialLowAlert` notification to the workshop's QC users (with the delivered material summary attached).
