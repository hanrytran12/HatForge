# Batch Workflow

## State Machine

```
Created (0)
  └─► Assigned (1)            [Admin creates batch, assigns Lead]
        └─► InProduction (2)   [Lead plans workshop chain]
              ├─► UnderQCReview (3)   [Staff submits work]
              │     └─► ReadyForTransfer (4)   [QC approves work]
              │           ├─► InProduction (2)   [QC confirms receipt at next workshop — mid-chain]
              │           └─► PendingLeadReview (6) [QC of last workshop tries to create transfer
              │                                       but no next hop exists → mark completed,
              │                                       Lead notified via FinalReviewRequested]
              └─► PendingLeadReview (6)  [Lead manually marks a workshop complete and it is the last
                                          remaining workshop]
                    └─► PendingGateQC (7)   [Lead approves final]
                          └─► Completed (5) [QC Gate confirms]
```

Status transitions are enforced inside service methods via `BusinessRuleException` — not via EF constraints.

---

## Step-by-Step Flow

### Step 1 — Admin Creates Batch
**Actor:** Admin
**Endpoint:** `POST /api/batch`
**Effect:**
- Validates `TargetQuantity > 0`, `StartDate >= today`, `EndDate > StartDate`
- Creates `Batch` with auto-generated `BatchNumber` (`BATCH-YYYYMMDD-XXXX`, sequential per day, scans for collisions)
- Status → `Assigned`
- Notification: Lead (`BatchAssigned`), Admins (`BatchCreated`)

---

### Step 2 — Lead Plans Workshop Chain
**Actor:** Lead (the assigned one)
**Endpoint:** `PUT /api/batch/{id}/plan`
**Effect:**
- Validates: ≥1 workshop, no duplicate `WorkshopId`, no duplicate `OrderIndex`, every workshop exists
- Per workshop: dates must be inside `[batch.StartDate, batch.EndDate]`, `EndDate > StartDate`
- For workshops that `RequiresMaterials = true`: `MaterialDeliveryDate` required (must be in `[batch.StartDate, workshop.StartDate]`), at least one `MaterialItems` entry, `EstimatedMetersPerUnit > 0`
- Wipes any prior `BatchWorkshop` rows for this batch (planning is fully replaceable while in `Assigned`)
- Normalizes `OrderIndex` to 0-based sequential in server-sorted order
- For each materials-requiring workshop: creates a `MaterialDelivery` (status `Scheduled`) and its `MaterialDeliveryItem` rows
- Status → `InProduction`
- Notification: per workshop, that workshop's QC staff (`BatchPlanned`)

---

### Step 3 — (Optional) QC Confirms Material Delivery
**Actor:** QCWorkshop (of the receiving workshop)
**Endpoint:** `PUT /api/material/confirm`
**Effect:**
- Records `ActualQuantity` per `MaterialDeliveryItem`
- Sets `BatchWorkshop.MaterialsReceived = true` and `InitialMaterialQty = SUM(ActualQuantity)`
- Sets `MaterialDelivery.Status = Received`
- Notification: Staff in workshop (`MaterialDeliveryConfirmed`)
- If the workshop is **first in the chain** and any item was short, auto-creates a `MaterialRequest` (`Pending`, Round 1) for the lead and notifies them (`MaterialShortfall`). The batch flow is **not** blocked by a shortfall — the workshop is unblocked as soon as the original delivery is confirmed.

**Constraint:** If `Workshop.RequiresMaterials = true`, Staff cannot submit work until `BatchWorkshop.MaterialsReceived = true`.

---

### Step 4 — Staff Submits Work
**Actor:** Staff (must belong to `dto.WorkshopId`)
**Endpoint:** `POST /api/work` (multipart/form-data, includes photo files; at least one photo required)
**Effect:**
- Estimates material usage: `estimatedUsage = quantity * BatchWorkshop.EstimatedMetersPerUnit` (0 if workshop doesn't require materials)
- Pre-charges `BatchWorkshop.MaterialUsed += estimatedUsage` so the budget is reserved up front
- Creates `Work` (status `Submitted`, `IsRework` from DTO, `EstimatedMaterialUsed` saved)
- Uploads photos to Cloudinary and creates `WorkPhoto` rows with `Type = Submission`
- Status → `UnderQCReview`
- Notification: workshop's QC staff (`WorkSubmitted`)

**Guards (throws `BusinessRuleException` if violated):**
- Batch must not be `Assigned` (planned) or `Completed`
- Staff must belong to the workshop they're submitting for
- If `Workshop.RequiresMaterials`: `BatchWorkshop.MaterialsReceived` must be `true`
- If `Workshop.RequiresMaterials` and there is no material remaining: blocks submit
- If `Workshop.RequiresMaterials` and `quantity * estimatedMetersPerUnit > remaining`: blocks submit with a rounded-meters error message
- For workshops after the first: a `TransferRequest` with status `Transferred` from the previous workshop must exist
- For non-rework submissions after the first workshop: `quantity` (plus previously-submitted non-rework quantity) cannot exceed the previous transfer's `ReceivedUsableQuantity` (capped; falls back to the source's `Work.PassedQuantity` if `ReceivedUsableQuantity` is null)
- Rework submissions don't consume the received-usable budget; quantity is capped by the remaining `RepairableQuantity` from prior rejections
- No existing pending (`Submitted`) Work for this batch+workshop

---

### Step 5 — QC Approves or Rejects Work
**Actor:** QCWorkshop
**Endpoints:** `PUT /api/work/approve` / `PUT /api/work/reject`
**Effect on approve:**
- Sets `Work.Status = Approved`, reviewer and timestamp
- Sets `PassedQuantity = Quantity`, `RepairableQuantity = 0`, `UnrepairableQuantity = 0`
- Reconciles `BatchWorkshop.MaterialUsed`: replaces the pre-charged estimate with `ActualMaterialUsed`
- Notification: Staff (`WorkApproved`, persisted), batch group + leads (`WorkApproved`, push only)
- If material remaining ≤ 5m after reconciliation: `MaterialLowAlert` to workshop's QC users

**Effect on reject:**
- `PassedQuantity + RepairableQuantity + UnrepairableQuantity` must equal `Quantity` (otherwise `BusinessRuleException`)
- Sets `Work.Status = Rejected`, reviewer, timestamp, `RejectionNotes`, and the QC quantity breakdown
- Reconciles `BatchWorkshop.MaterialUsed` the same way as approve
- Stores any optional rejection photos as `WorkPhoto { Type = Rejection }`
- Notification: Staff (`WorkRejected`, persisted)
- If material remaining ≤ 5m: `MaterialLowAlert` to workshop's QC users

**Batch status on reject:** remains `InProduction` so Staff can resubmit.

---

### Step 6 — QC Creates Transfer Request
**Actor:** QCWorkshop (of the source workshop)
**Endpoint:** `POST /api/transfer`
**Body:** `{ batchId }` only. Server auto-resolves the destination by `OrderIndex`.
**Effect:**
- Loads the source's `BatchWorkshop` and all Works for it
- Server auto-determines the next workshop by `OrderIndex`
- **If this is the last workshop** (no next `OrderIndex`):
  - Requires: `SUM(Work.PassedQuantity) > 0`, no `Submitted` work pending, no `RepairableQuantity` remaining
  - Marks source `BatchWorkshop.IsCompleted = true`
  - Status → `PendingLeadReview`
  - Notification: Lead (`FinalReviewRequested`)
  - Returns `{ IsFinalWorkshop: true, Transfer: null, BatchStatus: "PendingLeadReview" }`
- **Otherwise** (mid-chain):
  - Same prerequisites as the last-workshop path
  - Guards against an active (`Pending` or `Approved`) duplicate for the same `from → to` hop
  - Creates `TransferRequest { Status = Pending, CreatedByQCId = qcId }`
  - Notification: Lead (`TransferRequested`) — payload includes `ApprovedQuantity` (= sum of source's `Work.PassedQuantity`) and `QualityIssues` (any source Works with `UnrepairableQuantity > 0`)

**Guards:**
- Caller must be `QCWorkshop` and belong to a workshop in the chain
- Source `BatchWorkshop.IsCompleted` must be `false`
- `TransferRequestDto.QualityIssues` are sourced from rejected Works at the source workshop with `UnrepairableQuantity > 0`

---

### Step 7 — Lead Approves Transfer
**Actor:** Lead (any lead, but only the assigned lead can confirm/close the batch later)
**Endpoint:** `PUT /api/transfer/approve`
**Effect:**
- Sets `Status = Approved`, `ApprovedByLeadId`, `ApprovedAt`
- Notification: destination workshop's QC staff (`TransferApproved`, persisted per QC user)

**Constraint:** Transfer must be in `Pending`.

---

### Step 8 — Destination QC Confirms Receipt
**Actor:** QCWorkshop (of the destination workshop)
**Endpoint:** `PUT /api/transfer/confirm-receipt`
**Body:** `{ transferId, receivedUsableQuantity, receivedDefectiveQuantity, receiptInspectionNotes? }`
**Effect:**
- `receivedUsableQuantity + receivedDefectiveQuantity` must equal the approved transfer quantity (sum of source's `Work.PassedQuantity`); otherwise `BusinessRuleException`
- Records the inspection numbers and notes on the transfer
- Sets `Status = Transferred`, `ConfirmedByQCId`, `ConfirmedAt`
- Marks the source `BatchWorkshop.IsCompleted = true`
- Status → `InProduction` (mid-chain path only — last workshop uses Step 6's no-next path)
- Notification: destination workshop's staff (`WorkCanBegin`, persisted) with `receivedUsableQuantity`, `receivedDefectiveQuantity`, and `receiptDiscrepancyQuantity = approved - receivedUsable`

**Constraint:** Transfer must be `Approved`. Caller must belong to the destination workshop.

→ **Repeat Steps 4–8 for each subsequent workshop in the chain.**

---

## Lead Task Delegation Flow

When the assigned Lead is busy, they can delegate either a material delivery trip or a transfer approval trip to a QC Transport user. Delegation does not bypass Admin oversight.

1. Lead creates a delegation request through `POST /api/lead-task-delegation`.
   - `type = MaterialDelivery`: `taskId` is a `MaterialDelivery.Id`
   - `type = TransferApproval`: `taskId` is a `TransferRequest.Id`
2. Lead can track every request and its latest status through `GET /api/lead-task-delegation/my-requests`.
3. Admin reviews the request through `PUT /api/lead-task-delegation/{id}/approve` or `/reject`.
4. If rejected, the request becomes `Rejected`, the Lead receives `LeadTaskDelegationRejected`, and `adminNotes` is visible in `my-requests`. The original material delivery/transfer remains unchanged, so the Lead can handle it directly or create a new delegation.
5. If approved, the assigned QC Transport user receives `LeadTaskDelegationApproved`.
6. QC Transport executes the approved task:
   - `PUT /api/lead-task-delegation/{id}/material-delivered` marks the delivery as `Delivered`; workshop QC still confirms receipt through `/api/material/confirm`.
   - `PUT /api/lead-task-delegation/{id}/approve-transfer` approves the transfer on behalf of the requesting Lead; destination QC still confirms receipt through `/api/transfer/confirm-receipt`.
7. The delegation request becomes `Completed`, and Lead/Admin receive `LeadTaskDelegationCompleted`.

Guards:
- Only the assigned Lead for the batch can create the delegation.
- Assigned user must have role `QCTransport`.
- A target delivery/transfer cannot have another active (`PendingAdminApproval` or `Approved`) delegation.
- QC Transport can execute only requests assigned to them and approved by Admin.

---

### Step 9 — Lead Final Approval
**Actor:** Lead (the assigned one)
**Endpoint:** `PUT /api/batch/{id}/lead-approve`
**Effect:**
- Status → `PendingGateQC`
- Notification: All QCGate users (`GateQCReviewRequested`, persisted per QCGate user)

**Constraint:** Batch must be `PendingLeadReview`. Caller must be the assigned lead.

---

### Step 10 — QC Gate Final Confirmation
**Actor:** QCGate
**Endpoint:** `PUT /api/batch/{id}/gate-confirm`
**Effect:**
- Computes `Batch.CompletedQuantity = SUM(Work.PassedQuantity)` for the last workshop
- Status → `Completed`
- Sets `Batch.CompletedAt`
- Notification: Lead, Admins, batch subscribers (`BatchCompleted`, persisted for lead + admins)

**Constraint:** Batch must be `PendingGateQC`. Caller must be `QCGate`.

---

## Auxiliary Endpoints

### Mark Workshop Complete
`PUT /api/batch/{id}/workshops/{workshopId}/complete` — Role: `QCGate` or `Lead`
- Marks the specific `BatchWorkshop.IsCompleted = true`
- If all workshops in the chain are completed, sets `Batch.Status = PendingLeadReview` (Lead notified)
- If some are still incomplete, sets `Batch.Status = ReadyForTransfer`
- Rejects if the batch is still in `Assigned`

### Final Summary
`GET /api/batch/{id}/final-summary` — Role: `Lead` or `QCGate`
- Returns per-workshop submitted/approved/rejected work counts and approved quantity
- Returns per-workshop material budget (initial / used / remaining)
- Returns counts of `TransferRequest` and `MaterialRequest` by status

### Pending Gate QC
`GET /api/batch/pending-gate-qc` — Role: `QCGate`
- Lists batches in `PendingGateQC`

### My Batches
`GET /api/batch/my` — Role: `Lead`
- Lists batches assigned to the calling lead

---

## Material Top-Up Flows

These are off the main batch state machine but interact with `BatchWorkshop.MaterialUsed` and `InitialMaterialQty`.

### Shortfall request (auto, first-workshop only)
1. `MaterialDeliveryService.ConfirmDeliveryAsync` records actuals
2. If the workshop is `OrderIndex == 0` and any item was short, `MaterialRequestService.CreateRequestFromShortfallAsync` creates a `MaterialRequest` (`Pending`, `Round = previous + 1`)
3. Lead approves → `Approved`
4. Same workshop's QC confirms → `Fulfilled`; `BatchWorkshop.InitialMaterialQty += total delivered`
5. If the confirmation is still short, a new round is auto-spawned (subject to `MaxSupplementalRounds = 3`)

### Ad-hoc request (QC-initiated, first-workshop only)
1. `POST /api/material-request/ad-hoc` creates a `MaterialRequest { IsAdHoc = true, OriginalDeliveryId = null, Round = 1, Reason }`
2. Allowed batch statuses: `InProduction`, `UnderQCReview`, `ReadyForTransfer`, `PendingLeadReview`
3. Blocked if a prior `IsAdHoc` request is still `Pending` or `Approved` for the same workshop
4. Lead approves → QC confirms (same path as shortfall requests above)
5. A `Confirmed` ad-hoc request unblocks a new one

---

## BatchNumber Generation

Format: `BATCH-YYYYMMDD-XXXX` where `XXXX` is a zero-padded 4-digit sequence.

Sequence starts at `count + 1` for the day, then bumps on collision until a free slot is found.

Example: `BATCH-20240615-0001`, `BATCH-20240615-0002`

---

## Business Rule Violations → HTTP 400

All guards throw `BusinessRuleException` which `ErrorHandlingMiddleware` maps to 400.
Never return 200 with an error flag inside the response body for these cases.
