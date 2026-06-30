# Real-time Notifications (SignalR)

## Hub Endpoint

```
/hubs/notifications
```

Authentication: JWT passed via query string `?access_token=<token>`

---

## Client-Joinable Groups

Clients call these hub methods after connecting to subscribe to relevant group channels:

| Hub Method | Group Name | Who should call it |
|---|---|---|
| `JoinBatch(int batchId)` | `batch_{batchId}` | Anyone tracking a specific batch |
| `JoinWorkshop(int workshopId)` | `workshop_{workshopId}` | Staff / QC in that workshop |
| `JoinAdmins()` | `admins` | Admin users |
| `JoinLeads()` | `leads` | Lead users |
| `JoinQCGate()` | `qcgate` | QCGate users |
| `JoinUser(int userId)` | `user_{userId}` | Individual user (personal notifications) |

---

## Events & Target Groups

All events are pushed by `SignalRNotificationPublisher`. Some events also persist a `Notification` record to the DB for in-app notification history. The persisted events are tagged with their user-facing title.

| Event Name | SignalR Groups Notified | DB Persisted? | Trigger |
|---|---|---|---|
| `BatchAssigned` | `user_{leadId}`, `admins` | Yes (lead) | Admin creates batch |
| `BatchCreated` | `admins` | No | Same trigger as BatchAssigned |
| `BatchPlanned` | `workshop_{workshopId}` (one per workshop in chain) | Yes (each QC in each workshop) | Lead plans batch |
| `MaterialDeliveryConfirmed` | `batch_{batchId}`, `leads`, `workshop_{workshopId}` | Yes (each Staff in workshop) | QC confirms material delivery |
| `WorkSubmitted` | `workshop_{workshopId}` | No | Staff submits work |
| `WorkApproved` | `user_{staffId}`, `batch_{batchId}`, `leads` | Yes (staff) | QC approves work |
| `WorkRejected` | `user_{staffId}` | Yes (staff) | QC rejects work |
| `MaterialLowAlert` | `workshop_{workshopId}` | Yes (each QC in workshop) | Material remaining ≤ 5m after approve/reject |
| `TransferRequested` | `leads`, `user_{leadId}` | Yes (lead) | QC creates transfer request (mid-chain) |
| `TransferApproved` | `workshop_{toWorkshopId}`, `batch_{batchId}` | Yes (each QC in dest. workshop) | Lead approves transfer |
| `WorkCanBegin` | `workshop_{toWorkshopId}` | Yes (each Staff in dest. workshop) | QC confirms transfer receipt |
| `FinalReviewRequested` | `user_{leadId}` | Yes (lead) | Last workshop done → batch goes to `PendingLeadReview` |
| `GateQCReviewRequested` | `qcgate` | Yes (each QCGate user) | Lead approves final |
| `BatchCompleted` | `batch_{batchId}`, `admins`, `user_{leadId}` | Yes (admins + lead) | QC Gate confirms |
| `MaterialShortfall` | `user_{leadId}`, `leads` | Yes (lead) | Delivery confirmation creates shortfall request, or a fulfilled request is still short |
| `MaterialRequestApproved` | `workshop_{workshopId}`, `batch_{batchId}` | Yes (each QC in workshop) | Lead approves a material top-up request |
| `MaterialRequestFulfilled` | `user_{leadId}`, `leads` | Yes (lead) | Workshop confirms receipt of top-up materials |
| `AdHocMaterialRequest` | `user_{leadId}`, `leads` | Yes (lead) | QC creates an ad-hoc material request |

---

## INotificationPublisher Interface

```csharp
Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload);
Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload);
Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload);
Task NotifyTransferRequestedAsync(int leadId, object payload);
Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload);
Task NotifyBatchCompletedAsync(int batchId, int? leadId, object payload);
Task NotifyBatchAssignedToLeadAsync(int leadId, object payload);
Task NotifyBatchPlannedAsync(int workshopId, object payload);
Task NotifyMaterialDeliveryConfirmedAsync(int batchId, int workshopId, object payload);
Task NotifyWorkCanBeginAsync(int toWorkshopId, object payload);
Task NotifyFinalReviewRequestedAsync(int leadId, object payload);
Task NotifyGateQCReviewRequestedAsync(object payload);
Task NotifyMaterialShortfallAsync(int leadId, int batchId, int workshopId, object payload);
Task NotifyMaterialRequestApprovedAsync(int batchId, int workshopId, object payload);
Task NotifyMaterialRequestFulfilledAsync(int leadId, int batchId, int workshopId, object payload);
Task NotifyAdHocMaterialRequestAsync(int leadId, int batchId, int workshopId, object payload);
Task NotifyMaterialLowAlertAsync(int batchId, int workshopId, object payload);
```

Services depend on this interface. `SignalRNotificationPublisher` (API layer) implements it.
Tests use `NoOpNotificationPublisher`. Never reference the concrete class from Application or Domain.

---

## DB-Persisted Notifications

For events that persist to DB, `SignalRNotificationPublisher` creates a `Notification` record per target user. The record carries a `Type` (event name), `Title`, `Message`, and the JSON-serialized `Payload`.

The notification service exposes:

- `GET /api/notification` — fetch caller's notifications
- `GET /api/notification/unread-count` — unread count (returns `{ count }`)
- `PUT /api/notification/{id}/read` — mark one read
- `PUT /api/notification/read-all` — mark all read

Notification index `(UserId, IsRead)` is optimized for unread-count queries.