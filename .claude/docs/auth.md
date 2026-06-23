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
| `workshopId` | `User.WorkshopId ?? 0` | Custom string `"workshopId"` |

⚠️ The `workshopId` claim shape is fixed. `BaseApiController.CurrentWorkshopId` reads it directly. Changing the claim key or value type breaks all role-gated service calls.

---

## BaseApiController Helpers

All controllers inherit `BaseApiController`, which exposes:

```csharp
int CurrentUserId      // parsed from NameIdentifier claim
int CurrentWorkshopId  // parsed from custom "workshopId" claim (0 if null)
```

Services receive these values as method parameters — they do not access `IHttpContextAccessor`.

---

## Role Definitions

| Role | Value | Description |
|---|---|---|
| Admin | 0 | Creates batches, oversees everything. No workshop assignment. |
| Lead | 1 | Plans workshop chains, approves transfers, does final sign-off. No workshop assignment. |
| Staff | 2 | Submits work with photos. Must be assigned to a workshop. |
| QCWorkshop | 3 | Reviews work, initiates transfers, confirms receipts, confirms materials. Must be assigned to a workshop. |
| QCGate | 4 | Final gate quality confirmation. No workshop assignment. |

---

## Authorization Per Endpoint

### BatchController
| Action | Required Role |
|---|---|
| Create batch | Admin |
| Plan batch | Lead |
| Get my batches | Lead |
| Get all / get by id | Any authenticated |
| Mark workshop complete | QCGate, Lead |
| Lead approve final | Lead |
| Gate confirm | QCGate |

### WorkController
| Action | Required Role |
|---|---|
| Submit work | Staff |
| Approve work | QCWorkshop |
| Reject work | QCWorkshop |
| Get works | Any authenticated |

### TransferController
| Action | Required Role |
|---|---|
| Create transfer request | QCWorkshop |
| Approve transfer | Lead |
| Confirm receipt | QCWorkshop |
| Get pending / awaiting receipt | Lead / QCWorkshop |

### MaterialController
| Action | Required Role |
|---|---|
| Get pending deliveries | QCWorkshop |
| Confirm delivery | QCWorkshop |

### NotificationController
All actions: Any authenticated user (operates on caller's own notifications only).

---

## Password Policy (Registration)

Enforced by FluentValidation:
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit

Passwords are hashed with BCrypt (BCrypt.Net-Next). Plaintext is never stored or logged.

---

## Configuration

JWT settings live in `appsettings.json` under the `Jwt` section:

```json
{
  "Jwt": {
    "Key": "<secret>",
    "Issuer": "<issuer>",
    "Audience": "<audience>",
    "ExpiryDays": 7
  }
}
```

The secret key must be set via environment variable or `appsettings.Development.json` — never commit real secrets to source control.
