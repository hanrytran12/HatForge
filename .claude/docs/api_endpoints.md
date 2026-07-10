# API Endpoints

Base path: `/api`  
All non-auth endpoints require `Authorization: Bearer <token>`.  
All responses are wrapped: `{ success, data, error, errors }` (`ApiResponse<T>`).

---

## Auth — `/api/auth`

### POST `/api/auth/login`
No auth required.

**Request:**
```json
{ "email": "string", "password": "string" }
```
**Response `data`:**
```json
{ "token": "string", "userId": int, "name": "string", "email": "string", "role": "string", "workshopId": int|null }
```

`AuthController` currently exposes login only. User creation is Admin-only through `/api/user`.

---

## Admin Dashboard — `/api/admin-dashboard`

### GET `/api/admin-dashboard` — Role: Admin
**Response `data`:** `AdminDashboardDto` with KPIs, batch status counts, latest pending Lead delegations, and active-user/staff summaries.

---

## User — `/api/user`

### GET `/api/user` — Role: Admin
Returns active users only.
**Response `data`:** `UserDto[]`

### POST `/api/user` — Role: Admin

**Request:**
```json
{ "email": "string", "password": "string", "name": "string", "role": int, "workshopId": int|null }
```
`workshopId` is required for `Staff` and `QCWorkshop`; it must be null for Admin, Lead, QCGate, and QCTransport.
**Response `data`:** `UserDto`

### DELETE `/api/user/{id}` — Role: Admin
Soft-deletes an active Staff user (`IsActive = false`). This endpoint rejects non-Staff users and Staff whose workshop has active production work.
**Response `data`:** `{ }`

---

## Hat Model — `/api/hatmodel`

### GET `/api/hatmodel` — Any auth
**Response `data`:** `HatModelDto[]`

### POST `/api/hatmodel` — Role: Admin
**Request:**
```json
{ "name": "string", "description": "string|null" }
```
The server generates `code` as `HAT-YYYYMMDD-XXXX`.
**Response `data`:** `HatModelDto`

### PUT `/api/hatmodel/{id}` — Role: Admin
**Request:**
```json
{ "name": "string", "description": "string|null" }
```
**Response `data`:** `HatModelDto`

### DELETE `/api/hatmodel/{id}` — Role: Admin
Deletes a hat model only when no batch references it.
**Response `data`:** `{ }`

---

## Batch — `/api/batch`

### POST `/api/batch` — Role: Admin
**Request:**
```json
{ "hatModelId": int, "targetQuantity": int, "startDate": "datetime", "endDate": "datetime", "assignToLeadId": int }
```
**Response `data`:** `BatchDto`

### PUT `/api/batch/{id}/plan` — Role: Lead
**Request:**
```json
{
  "workshops": [
    {
      "workshopId": int,
      "orderIndex": int,                  // server normalizes to 0-based sequential
      "requiresMaterials": bool,
      "startDate": "datetime",
      "endDate": "datetime",
      "materialDeliveryDate": "datetime?",  // required when requiresMaterials = true
      "materialItems": [                    // required when requiresMaterials = true
        { "materialName": "string", "plannedQuantity": int, "unit": "string (default: m)" }
      ],
      "estimatedMetersPerUnit": decimal      // required (>0) when requiresMaterials = true
    }
  ]
}
```
**Response `data`:** `BatchDto`

Planning is allowed only while the batch is `Assigned`. `requiresMaterials` is decided per request item and becomes `BatchWorkshop.RequiresMaterials`; `Workshop.RequiresMaterials` is legacy/default metadata and does not block planning. For `requiresMaterials = true`, material delivery date, material items, and `estimatedMetersPerUnit > 0` are required. For `requiresMaterials = false`, material delivery date and material items must be omitted/empty and `estimatedMetersPerUnit` must be `0`. For material-requiring batch workshops, the server checks the assigned Lead's inventory by normalized `materialName + unit`, deducts the planned quantities only after the plan succeeds, and records `BatchPlanAllocation` stock transactions. If stock is missing or insufficient, the plan is rejected and no workshop plan is created.

### PUT `/api/batch/{id}/cancel` — Role: Admin
**Response `data`:** `BatchDto`

Cancels a batch only while it is still `Assigned`. Once Lead planning succeeds and the batch moves to `InProduction`, cancellation is rejected. Cancel does not restore inventory because successful planning is the first point where Lead inventory is deducted.

### GET `/api/batch/my` — Role: Lead
**Response `data`:** `BatchListDto[]`

### GET `/api/batch/pending-gate-qc` — Role: QCGate
**Response `data`:** `BatchListDto[]`

### GET `/api/batch` — Any auth
**Response `data`:** `BatchListDto[]`

### GET `/api/batch/{id}` — Any auth
**Response `data`:** `BatchDto` (includes `BatchWorkshops` with material budget fields)

### GET `/api/batch/{id}/final-summary` — Role: Lead, QCGate
**Response `data`:** `BatchFinalSummaryDto` (per-workshop work counts + material budget, transfer counts by status, material request counts by status)

### PUT `/api/batch/{id}/workshops/{workshopId}/complete` — Role: QCGate, Lead
**Response `data`:** `BatchDto`

### PUT `/api/batch/{id}/lead-approve` — Role: Lead
**Response `data`:** `BatchDto`

### PUT `/api/batch/{id}/gate-confirm` — Role: QCGate
**Response `data`:** `BatchDto` (with `CompletedQuantity` auto-computed from the last workshop's approved work)

---

## Work — `/api/work`

### POST `/api/work` — Role: Staff
`multipart/form-data`

| Field | Type |
|---|---|
| batchId | int |
| workshopId | int |
| quantity | int |
| isRework | bool |
| reportedMaterialUsed | decimal? (required >0 when this batch workshop requires materials; omitted/0 otherwise) |
| photos | IFormFile[] (≥1, allowed: `.jpg`, `.jpeg`, `.png`, `.webp`) |

**Response `data`:** `WorkDto` (`actualMaterialUsed`, `reportedMaterialUsed`, and `estimatedMaterialUsed` are included for material tracking)

For material-requiring batch workshops, `reportedMaterialUsed` reserves the workshop's batch stock and cannot exceed `BatchWorkshop.MaterialRemaining`. `estimatedMaterialUsed` is still stored as the planned baseline (`quantity * estimatedMetersPerUnit`) but does not reserve stock.

### PUT `/api/work/approve` — Role: QCWorkshop
**Request:**
```json
{ "workId": int, "actualMaterialUsed": decimal, "notes": "string (optional, currently unused)" }
```
**Response `data`:** `WorkDto`

`actualMaterialUsed` is the QC-finalized usage. For material-requiring batch workshops, approving reconciles `MaterialUsed` by releasing the Staff-reported reserve and applying the actual value. The actual value cannot exceed the workshop stock available after releasing that reserve.

### PUT `/api/work/reject` — Role: QCWorkshop
`multipart/form-data`

| Field | Type |
|---|---|
| workId | int |
| rejectionNotes | string (required, max 500) |
| passedQuantity | int |
| repairableQuantity | int |
| unrepairableQuantity | int |
| actualMaterialUsed | decimal |
| photos | IFormFile[] (optional) |

`passedQuantity + repairableQuantity + unrepairableQuantity` must equal the submitted `Quantity`.
For material-requiring batch workshops, rejecting reconciles material usage the same way as approval.

**Response `data`:** `WorkDto`

### GET `/api/work/batch/{batchId}` — Any auth
**Response `data`:** `WorkDto[]`

### GET `/api/work/batch/{batchId}/workshop/{workshopId}` — Any auth
**Response `data`:** `WorkDto[]`

---

## Transfer — `/api/transfer`

### POST `/api/transfer` — Role: QCWorkshop
**Request:**
```json
{ "batchId": int }
```
Server auto-resolves `toWorkshopId` from `OrderIndex`. Do not pass a target workshop.

**Response `data`:** `CreateTransferResultDto`:
```json
{
  "isFinalWorkshop": bool,            // true when no next workshop exists
  "transfer": TransferRequestDto|null,
  "batchStatus": "string"             // current batch status after the operation
}
```
When `isFinalWorkshop = true`, the transfer is not created and the batch moves directly to `PendingLeadReview`.

### PUT `/api/transfer/approve` — Role: Lead
**Request:**
```json
{ "transferId": int }
```
**Response `data`:** `TransferRequestDto`

### PUT `/api/transfer/confirm-receipt` — Role: QCWorkshop
**Request:**
```json
{
  "transferId": int,
  "receivedUsableQuantity": int,
  "receivedDefectiveQuantity": int,
  "receiptInspectionNotes": "string (optional, max 500)"
}
```
`receivedUsableQuantity + receivedDefectiveQuantity` must equal the approved transfer quantity (= sum of source's `Work.PassedQuantity`).

**Response `data`:** `TransferRequestDto` including `approvedQuantity`, `receivedUsableQuantity`, `receivedDefectiveQuantity`, `receiptDiscrepancyQuantity = approved - receivedUsable`, and `qualityIssues` (source rejections with `UnrepairableQuantity > 0`).

### GET `/api/transfer/pending` — Role: Lead
**Response `data`:** `TransferRequestDto[]`

### GET `/api/transfer/awaiting-receipt` — Role: QCWorkshop
Returns transfers where `ToWorkshopId == caller's workshopId` and status is `Approved`.  
**Response `data`:** `TransferRequestDto[]`

---

## Material — `/api/material`

### GET `/api/material/pending` — Role: QCWorkshop
Returns unconfirmed deliveries for the caller's workshop.  
**Response `data`:** `MaterialDeliveryDto[]`

### PUT `/api/material/confirm` — Role: QCWorkshop
**Request:**
```json
{
  "deliveryId": int,
  "items": [
    { "itemId": int, "actualQuantity": int }
  ]
}
```
**Response `data`:** `MaterialDeliveryDto`

`actualQuantity` cannot exceed the item's `plannedQuantity`.
If this planned batch workshop has `BatchWorkshop.RequiresMaterials = true` and any item was short, a `MaterialRequest` is auto-created; the batch flow itself is **not** blocked.
If the delivery has an active Lead task delegation for QCTransport, QCWorkshop can confirm receipt only after QCTransport marks the delivery as `Delivered`.

---

## Lead Inventory — `/api/lead-inventory`

All endpoints require role `Lead` and operate on the calling Lead's own central material stock.

### GET `/api/lead-inventory` — Role: Lead
Returns current stock rows grouped by normalized `materialName + unit`.
**Response `data`:** `LeadMaterialStockDto[]`

### GET `/api/lead-inventory/transactions` — Role: Lead
Returns the allocation/audit ledger ordered newest first.
**Response `data`:** `LeadMaterialStockTransactionDto[]`

### POST `/api/lead-inventory/stock-in` — Role: Lead
Adds quantity into inventory. If the material already exists with the same normalized name and unit, the quantity is incremented instead of creating a duplicate row.

**Request:**
```json
{
  "materialName": "string",
  "unit": "string",
  "quantity": decimal,
  "notes": "string|null"
}
```
**Response `data`:** `LeadMaterialStockDto`

### POST `/api/lead-inventory/adjust` — Role: Lead
Sets the on-hand quantity to an exact counted value. Use this for stock audits/corrections, not normal receiving. Example: if stock is currently `100` and `newQuantityOnHand = 92`, the resulting stock is `92` and the ledger records `-8`.

**Request:**
```json
{
  "materialName": "string",
  "unit": "string",
  "newQuantityOnHand": decimal,
  "reason": "string"
}
```
**Response `data`:** `LeadMaterialStockDto`

---

## Material Request — `/api/material-request`

### GET `/api/material-request/pending` — Role: Lead
**Response `data`:** `MaterialRequestDto[]` (filtered to caller's assigned batches)

### GET `/api/material-request/batch/{batchId}` — Any auth
**Response `data`:** `MaterialRequestDto[]`

### PUT `/api/material-request/{id}/approve` — Role: Lead
**Response `data`:** `MaterialRequestDto`

Approval validates and deducts the requested quantities from the assigned Lead's inventory, then records `MaterialRequestAllocation` transactions. If stock is missing or insufficient, the request remains `Pending`.

### POST `/api/material-request/ad-hoc` — Role: QCWorkshop
**Request:**
```json
{
  "batchId": int,
  "workshopId": int,
  "reason": "string (required, max 500)",
  "items": [
    { "materialName": "string", "unit": "string", "requestedQuantity": int }
  ]
}
```
Allowed for any workshop in the batch chain whose `BatchWorkshop.RequiresMaterials = true`, while the batch is in `InProduction`, `UnderQCReview`, `ReadyForTransfer`, or `PendingLeadReview`. `Workshop.RequiresMaterials` does not control ad-hoc eligibility. Blocked while a prior ad-hoc request is still `Pending` or `Approved` for the same batch workshop.

**Response `data`:** `MaterialRequestDto`

### PUT `/api/material-request/{id}/confirm` — Role: QCWorkshop
**Request:**
```json
{
  "requestId": int,                    // must match the route id
  "items": [
    { "itemId": int, "actualQuantity": int }
  ]
}
```
**Response `data`:** `MaterialRequestDto`. `actualQuantity` cannot exceed the requested shortfall quantity. If items are still short and rounds remain, a new `Pending` request is returned (`Round` incremented); otherwise the request becomes `Fulfilled` and the workshop's `InitialMaterialQty` is bumped.
If the material request has an active QCTransport delegation, QCWorkshop can confirm receipt only after QCTransport marks the request as delivered.
`MaterialRequestDto` includes `deliveredByTransportQcId`, `deliveredByTransportQcName`, and `deliveredAt` when supplemental material fulfillment was delivered through QCTransport delegation.

---

## Lead Task Delegation — `/api/lead-task-delegation`

Used when the assigned Lead is busy and wants a QC Transport user to perform material delivery, supplemental material delivery, transfer approval, or final batch review. The request must be approved by Admin before QC Transport can execute it.

`LeadTaskDelegationType`: `MaterialDelivery = 0`, `TransferApproval = 1`, `FinalReview = 2`, `MaterialRequestFulfillment = 3`  
`LeadTaskDelegationStatus`: `PendingAdminApproval = 0`, `Approved = 1`, `Rejected = 2`, `Completed = 3`

### POST `/api/lead-task-delegation` — Role: Lead
**Request:**
```json
{
  "type": 0,
  "taskId": int,
  "assignedTransportQcId": int,
  "reason": "string (optional, max 500)"
}
```
`taskId` is `materialDeliveryId` when `type = 0`; `transferRequestId` when `type = 1`; `batchId` when `type = 2`; `materialRequestId` when `type = 3`.

**Response `data`:** `LeadTaskDelegationDto`

### GET `/api/lead-task-delegation/pending-admin` — Role: Admin
**Response `data`:** `LeadTaskDelegationDto[]`

### GET `/api/lead-task-delegation/my-requests` — Role: Lead
Returns all delegation requests created by the caller, including `PendingAdminApproval`, `Approved`, `Rejected`, and `Completed`.
Rejected requests include `adminNotes` when Admin supplied a reason.
**Response `data`:** `LeadTaskDelegationDto[]`

### GET `/api/lead-task-delegation/my-assignments` — Role: QCTransport
Returns approved/completed delegations assigned to the caller.  
**Response `data`:** `LeadTaskDelegationDto[]`

### PUT `/api/lead-task-delegation/{id}/approve` — Role: Admin
**Request:** `{ "adminNotes": "string (optional, max 500)" }`  
**Response `data`:** `LeadTaskDelegationDto`

### PUT `/api/lead-task-delegation/{id}/reject` — Role: Admin
**Request:** `{ "adminNotes": "string (optional, max 500)" }`  
**Response `data`:** `LeadTaskDelegationDto`

### PUT `/api/lead-task-delegation/{id}/material-delivered` — Role: QCTransport
Marks the delegated `MaterialDelivery` as `Delivered` but not `Received`; workshop QC still confirms receipt through `/api/material/confirm`.  
**Response `data`:** `LeadTaskDelegationDto`

### PUT `/api/lead-task-delegation/{id}/approve-transfer` — Role: QCTransport
Approves the delegated transfer on behalf of the requesting Lead; destination workshop QC then continues through `/api/transfer/confirm-receipt`.  
**Response `data`:** `LeadTaskDelegationDto`

### PUT `/api/lead-task-delegation/{id}/approve-final-review` — Role: QCTransport
Approves final batch review on behalf of the requesting Lead for a delegated `FinalReview`; the batch moves from `PendingLeadReview` to `PendingGateQC`.  
**Response `data`:** `LeadTaskDelegationDto`

### PUT `/api/lead-task-delegation/{id}/material-request-delivered` — Role: QCTransport
Marks the delegated supplemental material request delivery as completed; workshop QC still confirms received quantities through `/api/material-request/{id}/confirm`.  
**Response `data`:** `LeadTaskDelegationDto`

---

## Notification — `/api/notification`

### GET `/api/notification` — Any auth
**Response `data`:** `NotificationDto[]`

### GET `/api/notification/unread-count` — Any auth
**Response `data`:** `{ "count": int }`

### PUT `/api/notification/{id}/read` — Any auth
**Response `data`:** `{ }` (empty object on success)

### PUT `/api/notification/read-all` — Any auth
**Response `data`:** `{ }`

---

## Error Responses

All errors follow the same envelope:
```json
{ "success": false, "data": null, "error": "message", "errors": ["field error 1"] }
```

| Exception | HTTP |
|---|---|
| NotFoundException | 404 |
| BusinessRuleException | 400 |
| ValidationException | 400 (includes `errors` array per field) |
| UnauthorizedException | 401 |
| ForbiddenException | 403 |
| Unhandled | 500 |

---

## API Documentation

- Swagger UI: `http://localhost:5235/swagger`
- Scalar.AspNetCore is referenced by the API project, but no Scalar route is currently mapped in `Program.cs`.
