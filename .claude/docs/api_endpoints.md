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
{ "token": "string", "userId": int, "name": "string", "role": "string", "workshopId": int|null }
```

### POST `/api/auth/register`
No auth required.

**Request:**
```json
{ "name": "string", "email": "string", "password": "string", "role": int, "workshopId": int|null }
```
**Response `data`:** same shape as login response.

---

## Batch — `/api/batch`

### POST `/api/batch` — Role: Admin
**Request:**
```json
{ "hatModelId": int, "assignedLeadId": int }
```
**Response `data`:** `BatchDto`

### PUT `/api/batch/{id}/plan` — Role: Lead
**Request:**
```json
{
  "plannedStartDate": "datetime",
  "plannedEndDate": "datetime",
  "workshops": [
    {
      "workshopId": int,
      "orderIndex": int,
      "startDate": "datetime",
      "endDate": "datetime",
      "materials": [
        { "materialName": "string", "plannedQuantity": decimal, "unit": "string" }
      ]
    }
  ]
}
```
**Response `data`:** `BatchDto`

### GET `/api/batch/my` — Role: Lead
**Response `data`:** `BatchDto[]`

### GET `/api/batch` — Any auth
**Response `data`:** `BatchDto[]`

### GET `/api/batch/{id}` — Any auth
**Response `data`:** `BatchDto` (includes `BatchWorkshops`, `MaterialDeliveries`)

### PUT `/api/batch/{id}/workshops/{workshopId}/complete` — Role: QCGate, Lead
**Response `data`:** `BatchDto`

### PUT `/api/batch/{id}/lead-approve` — Role: Lead
**Response `data`:** `BatchDto`

### PUT `/api/batch/{id}/gate-confirm` — Role: QCGate
**Response `data`:** `BatchDto`

---

## Work — `/api/work`

### POST `/api/work` — Role: Staff
`multipart/form-data`

| Field | Type |
|---|---|
| batchId | int |
| workshopId | int |
| quantity | int |
| notes | string (optional) |
| photos | IFormFile[] (optional, max size enforced by Cloudinary) |

Allowed photo types: `.jpg`, `.jpeg`, `.png`, `.webp`

**Response `data`:** `WorkDto`

### PUT `/api/work/approve` — Role: QCWorkshop
**Request:**
```json
{ "workId": int, "notes": "string (optional)" }
```
**Response `data`:** `WorkDto`

### PUT `/api/work/reject` — Role: QCWorkshop
`multipart/form-data`

| Field | Type |
|---|---|
| workId | int |
| rejectionNotes | string (required, max 500) |
| photos | IFormFile[] (optional) |

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

**Response `data`:** `TransferRequestDto` | null (null if last workshop → batch goes to PendingLeadReview directly)

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
  "receiptInspectionNotes": "string (optional)"
}
```
**Response `data`:** `TransferRequestDto` including `approvedQuantity`, `receivedUsableQuantity`, `receivedDefectiveQuantity`, `receiptDiscrepancyQuantity`, and `qualityIssues`.

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
    { "itemId": int, "actualQuantity": decimal }
  ]
}
```
**Response `data`:** `MaterialDeliveryDto`

---

## Notification — `/api/notification`

### GET `/api/notification` — Any auth
**Response `data`:** `NotificationDto[]`

### GET `/api/notification/unread-count` — Any auth
**Response `data`:** `int`

### PUT `/api/notification/{id}/read` — Any auth
**Response `data`:** `NotificationDto`

### PUT `/api/notification/read-all` — Any auth
**Response `data`:** `bool`

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
