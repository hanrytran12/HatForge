# Data Models

## Entity Relationship Overview

```
HatModel (1) ──────────────── (*) Batch
User [Lead] (1) ────────────── (*) Batch.AssignedToLead

Batch (1) ──────────────────── (*) BatchWorkshop ── (1) Workshop
Batch (1) ──────────────────── (*) Work
Batch (1) ──────────────────── (*) TransferRequest
Batch (1) ──────────────────── (*) MaterialDelivery

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
| AssignedLeadId | int | FK → User (Lead) |
| Status | BatchStatus | Persisted as int — do not renumber enum |
| PlannedStartDate | DateTime? | Set during planning |
| PlannedEndDate | DateTime? | Set during planning |
| CompletedAt | DateTime? | Set on Gate QC confirm |
| CreatedAt | DateTime | |
| BatchWorkshops | ICollection\<BatchWorkshop\> | Nav |
| Works | ICollection\<Work\> | Nav |
| TransferRequests | ICollection\<TransferRequest\> | Nav |
| MaterialDeliveries | ICollection\<MaterialDelivery\> | Nav |

### BatchWorkshop (join + state per workshop)
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| OrderIndex | int | Determines processing order |
| StartDate | DateTime? | Planned |
| EndDate | DateTime? | Planned |
| IsCompleted | bool | Marked true when workshop finishes |
| MaterialsReceived | bool | Set true on material delivery confirmation |

Unique index: `(BatchId, WorkshopId)`

### Workshop
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | |
| RequiresMaterials | bool | If true, Staff cannot submit work until `BatchWorkshop.MaterialsReceived = true` |
| Users | ICollection\<User\> | Nav |

### User
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| Name | string | Max 128 |
| Email | string | Unique, max 256 |
| PasswordHash | string | BCrypt |
| Role | UserRole | Admin=0, Lead=1, Staff=2, QCWorkshop=3, QCGate=4 |
| WorkshopId | int? | Null for Admin, Lead, QCGate |
| Workshop | Workshop? | Nav |

### Work
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| StaffId | int | FK → User (Staff) |
| ReviewedByQCId | int? | FK → User (QCWorkshop), nullable |
| Quantity | int | |
| Notes | string? | |
| RejectionNotes | string? | Max 500, required on rejection |
| Status | WorkStatus | Submitted=0, Approved=1, Rejected=2 |
| SubmittedAt | DateTime | |
| ReviewedAt | DateTime? | |
| Photos | ICollection\<WorkPhoto\> | Nav |

### WorkPhoto
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| WorkId | int | FK → Work |
| Url | string | Max 512, Cloudinary URL |
| Type | WorkPhotoType | Submission=0, Rejection=1 |

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
| TransferNote | string? | |
| ApprovalNote | string? | |
| ConfirmationNote | string? | |

### MaterialDelivery
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| BatchId | int | FK → Batch |
| WorkshopId | int | FK → Workshop |
| Status | MaterialDeliveryStatus | Scheduled=0, Delivered=1, Received=2 |
| ScheduledDate | DateTime? | |
| DeliveredAt | DateTime? | |
| ReceivedAt | DateTime? | |
| Items | ICollection\<MaterialDeliveryItem\> | Nav |

### MaterialDeliveryItem
| Property | Type | Notes |
|---|---|---|
| Id | int | PK |
| MaterialDeliveryId | int | FK → MaterialDelivery |
| MaterialName | string | Max 256 |
| PlannedQuantity | decimal | |
| ActualQuantity | decimal? | Set on confirmation |
| Unit | string | |

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
| Title | string | Max 256 |
| Message | string | Max 1024 |
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

### TransferStatus
```csharp
Pending = 0, Approved = 1, Transferred = 2
```

### MaterialDeliveryStatus
```csharp
Scheduled = 0, Delivered = 1, Received = 2
```

### UserRole
```csharp
Admin = 0, Lead = 1, Staff = 2, QCWorkshop = 3, QCGate = 4
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
| Photo.Url | Max 512 chars |
| RejectionNotes | Max 500 chars |
| MaterialName | Max 256 chars |
| Delete behavior | `Restrict` on Workshop/User FKs; `Cascade` on Batch child records; `SetNull` on optional reviewer/approver FKs |
