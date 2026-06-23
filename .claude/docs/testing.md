# Testing

## Stack

| Tool | Version | Purpose |
|---|---|---|
| xUnit | 2.9.3 | Test runner |
| Moq | 4.20.72 | Mocking |
| EF Core InMemory | 10.0.9 | In-process DB per test |
| FluentValidation.TestHelper | 12.1.1 | Validator unit tests |
| coverlet.collector | 6.0.4 | Code coverage |

---

## Test Project Structure

```
src/HatForge.Tests/
├── Fixtures/
│   ├── TestDataFactory.cs          — creates AppDbContext + UnitOfWork, seed helpers
│   └── NoOpNotificationPublisher.cs — no-op INotificationPublisher for service isolation
├── Unit/
│   ├── BatchServiceTests.cs        — 7 tests
│   ├── WorkServiceTests.cs         — 8 tests
│   ├── TransferServiceTests.cs     — 8 tests
│   └── ValidatorTests.cs           — 10 tests
└── Integration/
    └── BatchWorkflowTests.cs       — 2 tests (full end-to-end + rejection flow)
```

Total: 35 tests, 0 failures (as of latest run).

---

## Test Isolation Pattern

Each test creates a fresh EF InMemory database using a unique name:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;
var ctx = new AppDbContext(options);
var uow = new UnitOfWork(ctx);
```

This prevents state bleed between tests. Never share a context instance across tests.

---

## TestDataFactory — Seed Helpers

`TestDataFactory` provides static factory methods to seed minimal required entities:

| Method | Creates |
|---|---|
| `CreateLead(ctx)` | User with Role=Lead, no workshop |
| `CreateStaff(ctx, workshopId)` | User with Role=Staff, assigned to workshop |
| `CreateQcWorkshop(ctx, workshopId)` | User with Role=QCWorkshop |
| `CreateAdmin(ctx)` | User with Role=Admin |
| `CreateQcGate(ctx)` | User with Role=QCGate |
| `CreateWorkshop(ctx, requiresMaterials)` | Workshop entity |
| `CreateHatModel(ctx)` | HatModel entity |

Always call `ctx.SaveChangesAsync()` after seeding before instantiating services.

---

## NoOpNotificationPublisher

Implements `INotificationPublisher` with all methods returning `Task.CompletedTask`.  
Inject instead of `SignalRNotificationPublisher` in all unit and integration tests.  
This isolates service logic from SignalR and avoids DB writes for notifications in unit tests.

---

## Unit Test Coverage

### BatchServiceTests
- Create batch — generates correct `BatchNumber` format
- BatchNumber sequence increments correctly within same day
- Plan batch with workshops and materials
- Plan batch without materials (workshops that don't require them)
- Date validation (end before start → throws)
- Wrong lead guard (non-assigned lead → throws `ForbiddenException`)
- Invalid lead ID guard

### WorkServiceTests
- Submit work — first workshop (no transfer prerequisite)
- Submit work — second workshop (requires confirmed transfer receipt)
- Submit work blocked when materials not received
- Submit work blocked when duplicate pending submission exists
- Role guard (non-Staff → throws)
- Approve work — happy path
- Reject work — happy path with notes
- Double-approve guard (already approved → throws)

### TransferServiceTests
- Auto-determine next workshop by `OrderIndex`
- Guard: no approved work → throws
- Guard: workshop already completed → throws
- Guard: wrong workshop QC → throws
- Lead approves transfer — happy path
- Non-lead approve attempt → throws
- Confirm receipt — happy path (marks source completed, batch → InProduction)
- Confirm receipt before approval → throws
- Confirm receipt from wrong workshop → throws

### ValidatorTests
- Covers FluentValidation rules for: `CreateBatchRequest`, `PlanBatchRequest`, `SubmitWorkRequest`, `ApproveWorkRequest`, `RejectWorkRequest`, `CreateTransferRequest`, `ApproveTransferRequest`, `ConfirmReceiptRequest`, `ConfirmDeliveryRequest`, `LoginRequest`

---

## Integration Tests (BatchWorkflowTests)

### Full End-to-End Workflow
Exercises the complete 10-step flow in a single test using a 2-workshop chain:
1. Admin creates batch
2. Lead plans with 2 workshops (Cutting→Sewing), Cutting requires materials
3. QC1 confirms material delivery for Cutting
4. Staff1 submits work at Cutting
5. QC1 approves work at Cutting
6. QC1 creates transfer request (server resolves Sewing as next)
7. Lead approves transfer
8. QC2 confirms receipt at Sewing
9. Staff2 submits work at Sewing
10. QC2 approves work at Sewing
11. QC2 creates transfer (last workshop → batch goes to PendingLeadReview)
12. Lead approves final → PendingGateQC
13. QC Gate confirms → Completed

### Rejection Flow
Tests that a rejected work correctly resets to `InProduction` and allows Staff to resubmit.

---

## Running Tests

```bash
# Run all tests (single pass)
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=BatchServiceTests"
```

Do not use `--watch` in automated environments — it blocks indefinitely.
