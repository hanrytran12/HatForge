# Batch Workflow

## State Machine

```
Created (0)
  └─► Assigned (1)          [Admin creates batch, assigns Lead]
        └─► InProduction (2) [Lead plans workshop chain]
              └─► UnderQCReview (3)    [Staff submits work]
                    ├─► ReadyForTransfer (4)  [QC approves work]
                    │         └─► InProduction (2)  [QC confirms receipt at next workshop]
                    └─► PendingLeadReview (6) [Last workshop done, no more transfers]
                              └─► PendingGateQC (7)  [Lead approves final]
                                        └─► Completed (5)  [QC Gate confirms]
```

Status transitions are enforced inside service methods via `BusinessRuleException` — not via EF constraints.

---

## Step-by-Step Flow

### Step 1 — Admin Creates Batch
**Actor:** Admin  
**Endpoint:** `POST /api/batch`  
**Effect:**
- Creates `Batch` with auto-generated `BatchNumber` (`BATCH-YYYYMMDD-XXXX`, sequential per day)
- Assigns a Lead user
- Status → `Assigned`
- Notification: Lead notified (`BatchAssigned`), Admins notified (`BatchCreated`)

---

### Step 2 — Lead Plans Workshop Chain
**Actor:** Lead  
**Endpoint:** `PUT /api/batch/{id}/plan`  
**Effect:**
- Creates ordered `BatchWorkshop` entries with `OrderIndex` and planned dates
- Optionally creates `MaterialDelivery` + `MaterialDeliveryItem` records for workshops that `RequiresMaterials = true`
- Status → `InProduction`
- Notification: Each workshop's QC staff notified (`BatchPlanned`)

**Constraint:** Only the assigned Lead can call this. Batch must be in `Assigned` status.

---

### Step 3 — (Optional) QC Confirms Material Delivery
**Actor:** QCWorkshop (of the receiving workshop)  
**Endpoint:** `PUT /api/material/confirm`  
**Effect:**
- Records actual quantities per `MaterialDeliveryItem`
- Sets `BatchWorkshop.MaterialsReceived = true`
- Notification: Staff in workshop notified (`MaterialConfirmed`)

**Constraint:** If `Workshop.RequiresMaterials = true`, Staff cannot submit work until this step is complete.

---

### Step 4 — Staff Submits Work
**Actor:** Staff  
**Endpoint:** `POST /api/work` (multipart/form-data, includes photo files)  
**Effect:**
- Creates `Work` record with photos uploaded to Cloudinary
- Status → `UnderQCReview`
- Notification: Workshop QC notified (`WorkSubmitted`)

**Guards (throws `BusinessRuleException` if violated):**
- Batch must be `InProduction`
- Staff must belong to a workshop in the batch's chain
- If not the first workshop: a `TransferRequest` with status `Transferred` must exist for this workshop (receipt must have been confirmed), and normal work quantity cannot exceed the received usable quantity
- If `Workshop.RequiresMaterials = true`: `BatchWorkshop.MaterialsReceived` must be `true`
- No existing pending/approved Work for this batch+workshop combination

---

### Step 5 — QC Approves or Rejects Work
**Actor:** QCWorkshop  
**Endpoints:** `PUT /api/work/approve` / `PUT /api/work/reject`  
**Effect on approve:**
- Sets `Work.Status = Approved`, records reviewer and timestamp
- Status → `ReadyForTransfer`

**Effect on reject:**
- Sets `Work.Status = Rejected`, records rejection notes and optional rejection photos
- Status → `InProduction` (Staff can resubmit)
- Notification: Staff notified of result

**Constraint:** QC must belong to the same workshop as the Work.

---

### Step 6 — QC Creates Transfer Request
**Actor:** QCWorkshop (source workshop)  
**Endpoint:** `POST /api/transfer`  
**Effect:**
- Server auto-determines next workshop by `OrderIndex` (caller does not specify target)
- Creates `TransferRequest` with status `Pending`
- **If this is the last workshop** (no higher `OrderIndex` exists): skips transfer creation, batch goes directly to `PendingLeadReview`, Lead notified (`FinalReviewRequested`)
- **Otherwise:** Notification: Lead notified (`TransferRequested`)

**Guard:** Work for this batch+workshop must be `Approved`. Workshop must not already be marked `IsCompleted`.

---

### Step 7 — Lead Approves Transfer
**Actor:** Lead  
**Endpoint:** `PUT /api/transfer/approve`  
**Effect:**
- Sets `TransferRequest.Status = Approved`
- Notification: Destination workshop's QC notified (`TransferApproved`)

**Constraint:** Transfer must be in `Pending` status. Caller must be the assigned Lead of the batch.

---

### Step 8 — Destination QC Confirms Receipt
**Actor:** QCWorkshop (destination workshop)  
**Endpoint:** `PUT /api/transfer/confirm-receipt`  
**Effect:**
- Destination QC records `ReceivedUsableQuantity` and `ReceivedDefectiveQuantity`
- Receipt quantities must add up to the approved transfer quantity
- Sets `TransferRequest.Status = Transferred`
- Marks source `BatchWorkshop.IsCompleted = true`
- Status → `InProduction`
- Notification: Staff in destination workshop notified (`WorkCanBegin`) with receipt quantities
- Destination workshop non-rework submissions are capped by `ReceivedUsableQuantity`

**Constraint:** Transfer must be `Approved`. Caller must belong to the destination workshop.

→ **Repeat Steps 4–8 for each subsequent workshop in the chain.**

---

### Step 9 — Lead Final Approval
**Actor:** Lead  
**Endpoint:** `PUT /api/batch/{id}/lead-approve`  
**Effect:**
- Status → `PendingGateQC`
- Notification: All QCGate users notified (`GateQCReviewRequested`)

**Constraint:** Batch must be `PendingLeadReview`. Caller must be the assigned Lead.

---

### Step 10 — QC Gate Final Confirmation
**Actor:** QCGate  
**Endpoint:** `PUT /api/batch/{id}/gate-confirm`  
**Effect:**
- Status → `Completed`
- Sets `Batch.CompletedAt`
- Notification: Lead, Admins, and batch subscribers notified (`BatchCompleted`)

**Constraint:** Batch must be `PendingGateQC`.

---

## BatchNumber Generation

Format: `BATCH-YYYYMMDD-XXXX` where `XXXX` is a zero-padded 4-digit sequence.

Sequence resets per calendar day — computed by counting existing batches with the same date prefix and incrementing by 1.

Example: `BATCH-20240615-0001`, `BATCH-20240615-0002`

---

## Business Rule Violations → HTTP 400

All guards throw `BusinessRuleException` which `ErrorHandlingMiddleware` maps to 400.
Never return 200 with an error flag inside the response body for these cases.
