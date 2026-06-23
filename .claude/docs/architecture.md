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
  Entities, Enums, IRepository<T> interface (no external packages)  │
                                                                     │
HatForge.Infrastructure  ────────────────────────────────────────────┤
  AppDbContext, EF entity configs, generic Repository<T>,           │
  UnitOfWork, JwtService, CloudinaryFileStorageService              │
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
    IBatchRepository Batches;
    IRepository<Workshop> Workshops;
    IRepository<Work> Works;
    IRepository<TransferRequest> Transfers;
    IRepository<MaterialDelivery> MaterialDeliveries;
    IRepository<Notification> Notifications;
    IRepository<User> Users;
    Task<int> SaveChangesAsync();
}
```

`IRepository<T>` supports: `GetByIdAsync`, `FindAsync` (with eager-load string array), `FirstOrDefaultAsync`, `AddAsync`, `Update`, `Remove`.

No raw LINQ outside repositories. No `DbContext` references in Application or Domain.

### No CQRS / No MediatR

Services mix reads and writes. Controllers call services directly. Do not introduce MediatR.

### No AutoMapper

All DTO projection is manual, done inside each service via private static `MapToDto` or `MapToDtoValue` methods. Do not add AutoMapper.

### FluentValidation + ValidationFilter

`ValidationFilter` (action filter) resolves `IValidator<T>` from DI before the action executes. Throws `ValidationException` on failure. All validator classes are in `HatForge.Application/Validators/`.

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

### DbSeederHostedService

Registered as `IHostedService`. On startup:
1. Calls `ctx.Database.MigrateAsync()` (auto-applies pending EF migrations)
2. If no users exist, seeds default users, workshops, and a hat model

Do not disable or remove this service.

### DesignTimeDbContextFactory

Located in `HatForge.Infrastructure`. Exists solely for `dotnet ef` CLI tooling. Not used at runtime.

---

## Dependency Injection Registration

All registrations happen in `Program.cs` / extension methods in the API project:
- `AddScoped<IUnitOfWork, UnitOfWork>` (one per HTTP request)
- `AddScoped<I*Service, *Service>` for all Application services
- `AddScoped<INotificationPublisher, SignalRNotificationPublisher>`
- `AddScoped<IFileStorageService, CloudinaryFileStorageService>`
- `AddSingleton<IJwtService, JwtService>`
- FluentValidation validators auto-registered from `HatForge.Application` assembly

---

## BatchStatus Enum Integer Values

The `BatchStatus` enum has a deliberate gap and non-sequential ordering due to iterative development. The persisted integer values are fixed — do not renumber them.

```csharp
Created = 0
Assigned = 1
InProduction = 2
UnderQCReview = 3
ReadyForTransfer = 4
Completed = 5        ← gap: 6 and 7 were added later
PendingLeadReview = 6
PendingGateQC = 7
```
