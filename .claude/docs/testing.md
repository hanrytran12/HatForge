# Testing

## Stack

| Tool | Version | Purpose |
|---|---|---|
| xUnit | 2.9.3 | Test runner |
| Moq | 4.20.72 | Mocking (declared but unit tests primarily hand-roll fakes) |
| EF Core InMemory | 10.0.9 | In-process DB per test |
| FluentValidation.TestHelper | 12.1.1 | Validator unit tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test host |
| xunit.runner.visualstudio | 3.1.4 | VS test discovery |
| coverlet.collector | 6.0.4 | Code coverage |

---

## Test Project Structure

```
src/HatForge.Tests/
├── Fixtures/
│   ├── TestDataFactory.cs           — AppDbContext + UnitOfWork, seed helpers
│   └── NoOpNotificationPublisher.cs — no-op INotificationPublisher for service isolation
├── Unit/
│   ├── AdminDashboardServiceTests.cs — 1 test
│   ├── AuthServiceTests.cs          — 1 test
│   ├── BatchServiceTests.cs         — 12 tests
│   ├── HatModelServiceTests.cs      — 6 tests
│   ├── LeadInventoryServiceTests.cs — 4 tests
│   ├── LeadTaskDelegationServiceTests.cs — 16 tests
│   ├── MaterialRequestServiceTests.cs — 29 tests
│   ├── TransferServiceTests.cs      — 14 tests
│   ├── UserServiceTests.cs          — 6 tests
│   ├── ValidatorTests.cs            — 15 tests
│   └── WorkServiceTests.cs          — 24 tests
└── Integration/
    └── BatchWorkflowTests.cs        — 3 end-to-end tests
```

Total: 131 tests (`[Fact]` / `[Theory]` count).

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
| `CreateContext()` | AppDbContext backed by a fresh InMemory DB |
| `CreateUnitOfWork(ctx)` | Wraps the context in `UnitOfWork` |
| `Lead(id)` / `Staff(id, workshopId)` / `QcWorkshop(id, workshopId)` / `Admin(id)` / `QcGate(id)` / `QcTransport(id)` | User with role baked in |
| `Workshop(id, requiresMaterials)` | Workshop entity |
| `HatModel(id)` | HatModel entity |
| `LeadStock(...)` / `SeedLeadStockAsync(...)` | Lead central material stock for inventory allocation tests |
| `SeedBaseAsync(ctx)` | Seeds 1 Lead + 1 Staff + 1 QC + 1 Admin, 3 Workshops (workshop 3 requires materials), 1 HatModel |

Always call `SeedBaseAsync(ctx)` (or equivalent inserts) followed by `await ctx.SaveChangesAsync()` before instantiating services.

---

## NoOpNotificationPublisher

Implements every `INotificationPublisher` method as a no-op. The single exception is `NotifyMaterialLowAlertAsync`, which captures the last payload to `LastMaterialLowAlertPayload` so unit tests can assert on alert content (used by the `WorkServiceTests` material-low scenarios).

Inject instead of `SignalRNotificationPublisher` in all unit and integration tests. This isolates service logic from SignalR and avoids DB writes for notifications in unit tests.

---

## Unit Test Coverage

### BatchServiceTests
- Create batch — generates correct `BatchNumber` format
- BatchNumber sequence increments correctly within same day
- Create batch with end-before-start → throws `BusinessRuleException`
- Plan batch with workshops and materials
- Plan batch deducts assigned material from Lead inventory and writes stock ledger rows
- Plan batch with insufficient Lead inventory rejects and leaves no plan behind
- Plan batch with wrong lead → throws `ForbiddenException`
- Plan batch with `RequiresMaterials` but no delivery date → throws `BusinessRuleException`
- Create batch with invalid lead ID → throws `NotFoundException`

### AuthServiceTests
- Login rejects inactive users

### UserServiceTests
- Admin-created users are active by default
- Duplicate emails are rejected
- Staff and QCWorkshop users require a workshop
- Non-workshop roles reject workshop assignment
- Staff delete is a soft delete (`IsActive = false`)
- Delete blocks when the Staff user's workshop has active production work

### HatModelServiceTests
- Create generates a unique `HAT-YYYYMMDD-XXXX` code
- Lists models ordered by code
- Update trims and persists name/description
- Delete rejects models referenced by batches
- Delete missing model throws `NotFoundException`

### AdminDashboardServiceTests
- Dashboard returns KPIs, role counts, pending delegations, and staff work summaries

### LeadTaskDelegationServiceTests
- Lead creates Admin-reviewed delegations for material delivery, transfer approval, final review, and material request fulfillment
- Admin approve/reject paths update request status and notes
- QCTransport can execute only assigned, approved delegations
- Active duplicate delegations are blocked by service rules
- Delegated material delivery / supplemental fulfillment gates workshop QC receipt confirmation until transport marks delivered

### LeadInventoryServiceTests
- `StockInAsync` creates a new stock row and stock-in ledger entry
- `StockInAsync` normalizes material name/unit and increments an existing stock row
- `AdjustAsync` sets exact on-hand quantity and records the delta
- Non-Lead callers are rejected

### WorkServiceTests
- Submit work — first workshop, happy path (staff only, no materials gate)
- Submit work — non-Staff caller → `ForbiddenException`
- Submit work — `RequiresMaterials` but not received → `BusinessRuleException`
- Submit work — pre-charges `EstimatedMaterialUsed` against `MaterialUsed`
- Submit work — insufficient estimated material → `BusinessRuleException` with rounded-meters message
- Reject work — does not refund pre-charged estimate; reconciliation lands at `ActualMaterialUsed`
- Reject work — material low/out triggers `MaterialLowAlert` with delivered-material summary
- Approve work — reconciles estimate to `ActualMaterialUsed`
- Approve work — material low → alert with delivered-material summary
- Reject work — QC quantity breakdown (`Passed + Repairable + Unrepairable = Quantity`) allows repairable-only resubmission; rework submissions don't consume `ReceivedUsableQuantity`
- Reject work — quantities don't sum to `Quantity` → throws
- Submit work — second workshop without transferred status → throws
- Submit work — second workshop with transferred → succeeds; receipt usable cap enforced
- Submit work — exceeding `ReceivedUsableQuantity` after cumulative non-rework submissions → throws
- Approve work / reject work — happy path
- Approve already-approved → throws

### TransferServiceTests
- `CreateTransferRequestAsync` — auto-determines next workshop by `OrderIndex`
- Includes `UnrepairableQuantity > 0` rejections in `QualityIssues`
- Allows partial-pass / partial-unrepairable rejects when no `RepairableQuantity` remains
- Blocks creation while `RepairableQuantity` remains unsubmitted
- No approved work → throws
- Source already `IsCompleted` → throws
- QC of non-chain workshop → `NotFoundException`
- `ApproveTransferAsync` — happy path / non-lead → `ForbiddenException`
- `ConfirmReceiptAsync` — happy path: marks source completed, computes `ReceiptDiscrepancyQuantity`
- `ConfirmReceiptAsync` — before approval → throws
- `ConfirmReceiptAsync` — wrong workshop QC → `ForbiddenException`
- `ConfirmReceiptAsync` — receipt quantities must sum to approved
- `ConfirmReceiptAsync` — all-defective (`ReceivedUsable = 0`) is allowed

### MaterialRequestServiceTests
Shortfall flow (auto from delivery confirmation):
- Single-item short → creates `MaterialRequest` with one item, status `Pending`
- All items exact → no request created
- Shortfall on a non-first workshop → no request created
- Oversupply during delivery confirmation is rejected when actual quantity exceeds planned quantity
- `ApproveAsync` — assigned lead → validates/deducts Lead inventory, writes ledger, and sets `Approved`
- `ApproveAsync` — wrong lead → `ForbiddenException`
- `ApproveAsync` — already approved → throws
- `ApproveAsync` — insufficient Lead inventory leaves the request `Pending`
- `ConfirmAsync` — all satisfied → `Fulfilled`
- `ConfirmAsync` — still short → auto-creates next round, returns the new pending DTO
- `ConfirmAsync` — wrong workshop QC → `ForbiddenException`
- `ConfirmAsync` — beyond max rounds (4th) → throws
- `GetPendingForLeadAsync` — filters by `AssignedToLeadId`
- Batch flow unblocked by shortfall (delivery confirmation still flips `MaterialsReceived = true`)

Ad-hoc flow:
- QC of first materials-requiring workshop creates request with `IsAdHoc = true`, `Round = 1`
- Non-first workshop → throws
- Wrong-workshop QC → `ForbiddenException`
- Non-QC staff → `ForbiddenException`
- Batch status `Completed` → throws
- Workshop without `RequiresMaterials` → throws
- Workshop not in chain → throws
- Open `Pending` or `Approved` ad-hoc blocks new ones
- After fulfillment, a new ad-hoc is allowed
- Full ad-hoc lifecycle (create → approve → confirm) completes

### ValidatorTests
FluentValidation rules: `CreateBatchRequest`, `PlanBatchRequest`, `SubmitWorkRequest`, `RejectWorkRequest`, `CreateTransferRequest`, `ConfirmReceiptRequest` (negative quantities, valid case with notes).

---

## Integration Tests (BatchWorkflowTests)

End-to-end coverage of the main state machine:

1. **FullWorkflow_CreatePlanSubmitApproveTransferComplete** — 2-workshop chain, exercises every step through `Completed`, including the last-workshop transfer becoming `PendingLeadReview`.
2. **TransferReceiptDiscrepancy_CapsDestinationWorkQuantity** — when the destination QC confirms `ReceivedUsableQuantity = 95, ReceivedDefectiveQuantity = 5`, the next workshop cannot submit 100 non-rework items; 95 succeeds.
3. **RejectionFlow_RejectedWorkRecordsReason** — reject flow stores `RejectionNotes` and sets `Status = Rejected`.

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
