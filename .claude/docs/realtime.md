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

All events are pushed by `SignalRNotificationPublisher`. Some events also persist a `Notification` record to the DB for in-app notification history.

| Event Name | Groups Notified | DB Notification Persisted? | Trigger |
|---|---|---|---|
| `WorkSubmitted` | `workshop_{workshopId}` | No | Staff submits work |
| `WorkApproved` | `user_{staffId}`, `batch_{batchId}`, `leads` | Yes (for staff) | QC approves work |
| `WorkRejected` | `user_{staffId}` | Yes (for staff) | QC rejects work |
| `TransferRequested` | `leads`, `user_{leadId}` | Yes (for lead) | QC creates transfer request |
| `TransferApproved` | `workshop_{toWorkshopId}`, `batch_{batchId}` | Yes (for each QC in dest. workshop) | Lead approves transfer |
| `MaterialConfirmed` | `batch_{batchId}`, `leads`, `workshop_{workshopId}` | Yes (for each Staff in workshop) | QC confirms material delivery |
| `WorkCanBegin` | `workshop_{toWorkshopId}` | Yes (for each Staff in workshop) | QC confirms transfer receipt |
| `BatchAssigned` | `user_{leadId}`, `admins` | Yes (for lead) | Admin creates batch |
| `BatchCreated` | `admins` | No | Same trigger as BatchAssigned |
| `BatchPlanned` | `workshop_{workshopId}` (one per workshop in chain) | Yes (for each QC in each workshop) | Lead plans batch |
| `FinalReviewRequested` | `user_{leadId}` | Yes (for lead) | Last workshop transfer skipped → PendingLeadReview |
| `GateQCReviewRequested` | `qcgate` | Yes (for each QCGate user) | Lead approves final |
| `BatchCompleted` | `batch_{batchId}`, `admins`, `user_{leadId}` | Yes (for admins + lead) | QC Gate confirms |

---

## INotificationPublisher Interface

```csharp
Task NotifyWorkSubmittedAsync(int batchId, int workshopId, string batchNumber);
Task NotifyWorkApprovedAsync(int batchId, int workshopId, int staffId, string batchNumber);
Task NotifyWorkRejectedAsync(int batchId, int workshopId, int staffId, string batchNumber, string notes);
Task NotifyTransferRequestedAsync(int batchId, int leadId, string batchNumber, string fromWorkshop, string toWorkshop);
Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, string batchNumber, string toWorkshop);
Task NotifyMaterialConfirmedAsync(int batchId, int workshopId, string batchNumber, string workshopName);
Task NotifyWorkCanBeginAsync(int batchId, int toWorkshopId, string batchNumber, string workshopName);
Task NotifyBatchAssignedAsync(int batchId, int leadId, string batchNumber);
Task NotifyBatchPlannedAsync(int batchId, List<int> workshopIds, string batchNumber);
Task NotifyFinalReviewRequestedAsync(int batchId, int leadId, string batchNumber);
Task NotifyGateQCReviewRequestedAsync(int batchId, string batchNumber);
Task NotifyBatchCompletedAsync(int batchId, int leadId, string batchNumber);
```

Services depend on this interface. `SignalRNotificationPublisher` (API layer) implements it.  
Tests use `NoOpNotificationPublisher`. Never reference the concrete class from Application or Domain.

---

## DB-Persisted Notifications

For events that persist to DB, `SignalRNotificationPublisher` creates a `Notification` record per target user. The notification service exposes:

- `GET /api/notification` — fetch caller's notifications
- `GET /api/notification/unread-count` — unread count
- `PUT /api/notification/{id}/read` — mark one read
- `PUT /api/notification/read-all` — mark all read

Notification index `(UserId, IsRead)` is optimized for unread-count queries.
