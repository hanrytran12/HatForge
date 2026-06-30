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

### POST `/api/auth/register`
No auth required.

**Request:**
```json
{ "name": "string", "email": "string", "password": "string", "role": int, "workshopId": int|null }
```
**Response `data`:** user DTO (same shape minus `token`).

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
        { "materialName": "string", "plannedQuantity": int }
      ],
      "estimatedMetersPerUnit": decimal      // required (>0) when requiresMaterials = true
    }
  ]
}
```
**Response `data`:** `BatchDto`

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
| photos | IFormFile[] (≥1, allowed: `.jpg`, `.jpeg`, `.png`, `.webp`) |

**Response `data`:** `WorkDto`

### PUT `/api/work/approve` — Role: QCWorkshop
**Request:**
```json
{ "workId": int, "actualMaterialUsed": decimal, "notes": "string (optional, currently unused)" }
```
**Response `data`:** `WorkDto`

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

If the workshop is the first in the batch chain and any item was short, a `MaterialRequest` is auto-created; the batch flow itself is **not** blocked.

---

## Material Request — `/api/material-request`

### GET `/api/material-request/pending` — Role: Lead
**Response `data`:** `MaterialRequestDto[]` (filtered to caller's assigned batches)

### GET `/api/material-request/batch/{batchId}` — Any auth
**Response `data`:** `MaterialRequestDto[]`

### PUT `/api/material-request/{id}/approve` — Role: Lead
**Response `data`:** `MaterialRequestDto`

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
Allowed only for the first workshop in the chain whose `Workshop.RequiresMaterials = true`, while the batch is in `InProduction`, `UnderQCReview`, `ReadyForTransfer`, or `PendingLeadReview`. Blocked while a prior ad-hoc request is still `Pending` or `Approved`.

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
**Response `data`:** `MaterialRequestDto`. If items are still short and rounds remain, a new `Pending` request is returned (`Round` incremented); otherwise the request becomes `Fulfilled` and the workshop's `InitialMaterialQty` is bumped.

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
- Scalar UI: available via Scalar.AspNetCore (check `Program.cs` for route)