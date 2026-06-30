# Architecture

## Layer Structure

5-project Clean Architecture. Dependency direction is strictly inward.

```
HatForge.API  ──────────────────────────────────────────────────────┐
  Controllers, SignalR hub, Middleware, DI registration, DbSeeder   │
  ↓ depends on                                                       │
HatForge.Application  ──────────────────────────────────────────────┤
  Services, DTOs, Validators, Interfaces (INotificationPublisher,   │
  IUnitOfWork, IRepository<T>, IFileStorageService, etc.)           │
  ↓ depends on                                                       │
HatForge.Domain  ───────────────────────────────────────────────────┤
  Entities, Enums, IRepository<T> interface, IUnitOfWork interface  │
  (no external packages)                                            │
                                                                     │
HatForge.Infrastructure  ────────────────────────────────────────────┤
  AppDbContext, EF entity configs, generic Repository<T>,           │
  UnitOfWork, JwtTokenGenerator, CloudinaryFileStorageService       │
  ↓ implements Application interfaces                               │
                                                                     │
HatForge.Tests  ────────────────────────────────────────────────────┘
  xUnit, Moq, EF InMemory
```

---

## Key Patterns

### Repository + Unit of Work

All data access goes through `IUnitOfWork`, which exposes typed repository properties:

```csharp
IUnitOfWork {
    IRepository<Batch>               Batches;
    IRepository<Work>                Works;
    IRepository<WorkPhoto>           WorkPhotos;
    IRepository<TransferRequest>     TransferRequests;
    IRepository<MaterialDelivery>    MaterialDeliveries;
    IRepository<MaterialDeliveryItem> MaterialDeliveryItems;
    IRepository<MaterialRequest>     MaterialRequests;
    IRepository<MaterialRequestItem> MaterialRequestItems;
    IRepository<BatchWorkshop>       BatchWorkshops;
    IRepository<Workshop>           Workshops;
    IRepository<HatModel>           HatModels;
    IRepository<User>                Users;
    IRepository<Notification>        Notifications;
    Task<int> SaveChangesAsync();
}
```

`IRepository<T>` supports: `GetByIdAsync`, `FindAsync` (with eager-load string array), `FirstOrDefaultAsync`, `AddAsync`, `Update`, `Remove`.

No raw LINQ outside repositories. No `DbContext` references in Application or Domain.

### No CQRS / No MediatR

Services mix reads and writes. Controllers call services directly. Do not introduce MediatR.

### No AutoMapper

All DTO projection is manual, done inside each service via private static methods (`MapToDto`, `MapToDtoValue`, etc.). Do not add AutoMapper.

### FluentValidation + ValidationFilter

`ValidationFilter` (action filter) resolves `IValidator<T>` from DI before the action executes. Throws `ValidationException` on failure. All validator classes live in `HatForge.Application/Validators/Validators.cs`.

### Global Error Middleware

`ErrorHandlingMiddleware` (registered first in pipeline) catches all exceptions and returns `ApiResponse<T>`:

| Exception | HTTP Status |
|---|---|
| `NotFoundException` | 404 |
| `BusinessRuleException` | 400 |
| `ValidationException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| Any other | 500 |

All custom exception types are defined in `HatForge.Application/Common/Exceptions.cs`.

### ApiResponse<T> Envelope

Every controller action returns `ApiResponse<T>`. Never return raw objects.

```csharp
{ success: bool, data: T, error: string, errors: string[] }
```

### INotificationPublisher Abstraction

Services depend on `INotificationPublisher` (Application layer). The real implementation `SignalRNotificationPublisher` lives in the API layer and is never referenced by services. Tests inject `NoOpNotificationPublisher`. This is intentional — do not couple services to SignalR.

The publisher owns both SignalR group fan-out and DB notification persistence (`Notification` rows) for events that should appear in the in-app notification feed.

### DbSeederHostedService

Registered as `IHostedService`. On startup:
1. Calls `ctx.Database.MigrateAsync()` (auto-applies pending EF migrations)
2. If no users exist, seeds default users, workshops, and a hat model
3. Seeds 9 default accounts (Admin, Lead, 3 Staff + 3 QCWorkshop, 1 QCGate) — see `DbSeeder.cs`

Do not disable or remove this service.

### DesignTimeDbContextFactory

Located in `HatForge.Infrastructure`. Exists solely for `dotnet ef` CLI tooling. Not used at runtime.

---

## Dependency Injection Registration

All registrations happen in `Program.cs` / extension methods in the API project:
- `AddScoped<IUnitOfWork, UnitOfWork>` (one per HTTP request)
- `AddScoped<I*Service, *Service>` for all Application services (`BatchService`, `WorkService`, `TransferService`, `MaterialDeliveryService`, `MaterialRequestService`, `AuthService`, `NotificationService`)
- `AddScoped<INotificationPublisher, SignalRNotificationPublisher>`
- `AddScoped<IFileStorageService, CloudinaryFileStorageService>`
- `AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>`
- `AddScoped<IPasswordHasher, PasswordHasher>`
- FluentValidation validators auto-registered from `HatForge.Application` assembly

---

## BatchStatus Enum Integer Values

The `BatchStatus` enum has a deliberate gap and non-sequential ordering due to iterative development. The persisted integer values are fixed — do not renumber them.

```csharp
Created           = 0
Assigned          = 1
InProduction      = 2
UnderQCReview     = 3
ReadyForTransfer  = 4
Completed         = 5        ← gap: 6 and 7 were added later
PendingLeadReview = 6
PendingGateQC     = 7
```

---

## Material Tracking

Constants live in `Application/Common/MaterialTracking.cs`:

```csharp
LowMaterialThresholdMeters = 5m
```

`WorkService` reconciles `BatchWorkshop.MaterialUsed` against the QC-measured `ActualMaterialUsed` after every approve/reject, and emits a `MaterialLowAlert` (via `INotificationPublisher`) when the remaining budget drops to or below the threshold.

Shortfall and ad-hoc top-up requests are managed by `MaterialRequestService`. Maximum 3 supplemental rounds (`MaxSupplementalRounds = 3`); a fourth still-short confirmation throws `BusinessRuleException`.