# Authentication & Authorization

## Mechanism

JWT Bearer (HS256). All tokens expire after **7 days**. No refresh token mechanism exists.

Clients must send: `Authorization: Bearer <token>`

For SignalR connections: token passed via query string `?access_token=<token>`

---

## JWT Claims Shape

| Claim | Source | Key in code |
|---|---|---|
| `NameIdentifier` | `User.Id` | `ClaimTypes.NameIdentifier` |
| `Email` | `User.Email` | `ClaimTypes.Email` |
| `Name` | `User.Name` | `ClaimTypes.Name` |
| `Role` | `User.Role.ToString()` | `ClaimTypes.Role` |
| `workshopId` | `User.WorkshopId?.ToString() ?? ""` | Custom string `"workshopId"` |

⚠️ The `workshopId` claim shape is fixed. `BaseApiController.CurrentWorkshopId` parses it as `int?` (null when missing). Changing the claim key or value type breaks all role-gated service calls.

---

## BaseApiController Helpers

All controllers inherit `BaseApiController`, which exposes:

```csharp
int CurrentUserId      // parsed from NameIdentifier claim
int? CurrentWorkshopId // parsed from custom "workshopId" claim (null when absent)
```

Services receive these values as method parameters — they do not access `IHttpContextAccessor`.

---

## Role Definitions

| Role | Value | Description |
|---|---|---|
| Admin | 0 | Creates batches, oversees everything. No workshop assignment. |
| Lead | 1 | Plans workshop chains, approves transfers, does final sign-off. No workshop assignment. |
| Staff | 2 | Submits work with photos. Must be assigned to a workshop. |
| QCWorkshop | 3 | Reviews work, initiates transfers, confirms receipts, confirms materials, creates ad-hoc material requests. Must be assigned to a workshop. |
| QCGate | 4 | Final gate quality confirmation. No workshop assignment. |
| QCTransport | 5 | Performs Admin-approved Lead delegation tasks for material delivery and transfer approval. No workshop assignment. |

---

## Authorization Per Endpoint

### AuthController
| Action | Required Role |
|---|---|
| Login | Anonymous |
| Register | Anonymous |

### BatchController
| Action | Required Role |
|---|---|
| `POST /api/batch` | Admin |
| `PUT /api/batch/{id}/plan` | Lead |
| `GET /api/batch/my` | Lead |
| `GET /api/batch/pending-gate-qc` | QCGate |
| `GET /api/batch` (all) | Any authenticated |
| `GET /api/batch/{id}` | Any authenticated |
| `GET /api/batch/{id}/final-summary` | Lead or QCGate |
| `PUT /api/batch/{id}/workshops/{workshopId}/complete` | QCGate or Lead |
| `PUT /api/batch/{id}/lead-approve` | Lead |
| `PUT /api/batch/{id}/gate-confirm` | QCGate |

### WorkController
| Action | Required Role |
|---|---|
| `POST /api/work` (submit) | Staff |
| `PUT /api/work/approve` | QCWorkshop |
| `PUT /api/work/reject` | QCWorkshop |
| `GET /api/work/batch/{batchId}` | Any authenticated |
| `GET /api/work/batch/{batchId}/workshop/{workshopId}` | Any authenticated |

### TransferController
| Action | Required Role |
|---|---|
| `POST /api/transfer` | QCWorkshop |
| `PUT /api/transfer/approve` | Lead |
| `PUT /api/transfer/confirm-receipt` | QCWorkshop (destination workshop) |
| `GET /api/transfer/pending` | Lead |
| `GET /api/transfer/awaiting-receipt` | QCWorkshop |

### MaterialController
| Action | Required Role |
|---|---|
| `GET /api/material/pending` | QCWorkshop |
| `PUT /api/material/confirm` | QCWorkshop |

### MaterialRequestController
| Action | Required Role |
|---|---|
| `GET /api/material-request/pending` | Lead |
| `GET /api/material-request/batch/{batchId}` | Any authenticated |
| `PUT /api/material-request/{id}/approve` | Lead |
| `POST /api/material-request/ad-hoc` | QCWorkshop |
| `PUT /api/material-request/{id}/confirm` | QCWorkshop |

### LeadTaskDelegationController
| Action | Required Role |
|---|---|
| `POST /api/lead-task-delegation` | Lead |
| `GET /api/lead-task-delegation/pending-admin` | Admin |
| `GET /api/lead-task-delegation/my-requests` | Lead |
| `GET /api/lead-task-delegation/my-assignments` | QCTransport |
| `PUT /api/lead-task-delegation/{id}/approve` | Admin |
| `PUT /api/lead-task-delegation/{id}/reject` | Admin |
| `PUT /api/lead-task-delegation/{id}/material-delivered` | QCTransport |
| `PUT /api/lead-task-delegation/{id}/approve-transfer` | QCTransport |
| `PUT /api/lead-task-delegation/{id}/approve-final-review` | QCTransport |

### NotificationController
All actions: any authenticated user (operates on caller's own notifications only).

---

## Password Policy (Registration)

Enforced by FluentValidation (`RegisterValidator`):
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit

Passwords are hashed with BCrypt (BCrypt.Net-Next). Plaintext is never stored or logged.

---

## Seeded Accounts (Development)

`DbSeederHostedService` creates the following accounts on a fresh database:

| Email | Role | Workshop | Password |
|---|---|---|---|
| admin@hatforge.com | Admin | — | `Admin123!` |
| lead@hatforge.com | Lead | — | `Lead123!` |
| staff@hatforge.com | Staff | 1 (Cutting) | `Staff123!` |
| staff2@hatforge.com | Staff | 2 (Sewing) | `Staff123!` |
| staff3@hatforge.com | Staff | 3 (Finishing) | `Staff123!` |
| qc1@hatforge.com | QCWorkshop | 1 (Cutting) | `Qc123!` |
| qc2@hatforge.com | QCWorkshop | 2 (Sewing) | `Qc123!` |
| qc3@hatforge.com | QCWorkshop | 3 (Finishing) | `Qc123!` |
| transport@hatforge.com | QCTransport | — | `Transport123!` |
| gate@hatforge.com | QCGate | — | `Gate123!` |

Seeded workshops: `Cutting` (RequiresMaterials=true), `Sewing` (false), `Finishing` (false).

---

## Configuration

JWT settings live in `appsettings.json` under the `Jwt` section:

```json
{
  "Jwt": {
    "Secret": "<secret>",
    "Issuer": "<issuer>",
    "Audience": "<audience>"
  }
}
```

Tokens are issued with `Expires = DateTime.UtcNow.AddDays(7)` regardless of `ExpiryDays` configuration (the constant lives in `JwtTokenGenerator.cs`).

The secret must be set via environment variable or `appsettings.Development.json` — never commit real secrets to source control.
