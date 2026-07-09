# HatForge

Real-time manufacturing batch orchestration API for Vietnamese hat production facilities.

## Problem

Hat manufacturers run multi-stage workshop workflows on paper. There's no real-time visibility into which
batch is at which stage, who approved what, or how much usable output actually survived each transfer.

## Solution

A web API that digitizes the full production chain — from batch planning to final gate-QC — with photo
evidence, QC quantity breakdowns, material budgeting, and push notifications at every transition.

## Features

- **Multi-stage batch tracking** through an ordered chain of workshops with role-gated approvals
- **Staff submission with photo evidence** (Cloudinary) and rework resubmission
- **QC review with quantity breakdown** — `passed / repairable / unrepairable` per work item, surfaced as quality issues on transfer
- **Receipt inspection** — destination QC records usable vs. defective counts and inspection notes; non-rework submissions downstream are capped by received-usable quantity
- **Material delivery + top-up workflow** — scheduled deliveries with actual quantities, auto-spawned shortfall requests (max 3 supplemental rounds), QC-initiated ad-hoc requests
- **Material budgeting** — pre-charged estimate per submission, reconciled to QC-measured actual, low-stock alerts at 5m remaining
- **Admin-approved Lead delegation** to QCTransport for material delivery, supplemental fulfillment, transfer approval, and final review
- **Real-time push notifications** via SignalR (`/hubs/notifications`) plus a persisted in-app notification feed
- **Role-based access control** across 6 roles: Admin, Lead, Staff, QCWorkshop, QCGate, QCTransport
- **Clean Architecture** (Domain → Application → Infrastructure → API) with no AutoMapper, no MediatR, no DbContext in services

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET | 10.0 |
| Web framework | ASP.NET Core (Web API) | 10.0 |
| ORM | Entity Framework Core | 10.0.9 |
| Database | PostgreSQL (Npgsql) | 10.0.2 |
| Real-time | SignalR | — |
| Authentication | JWT Bearer (HS256, 7-day) | 10.0.9 |
| Password hashing | BCrypt.Net-Next | 4.2.0 |
| Validation | FluentValidation | 12.1.1 |
| File storage | Cloudinary | 1.27.1 |
| API docs | Swashbuckle UI; Scalar package referenced but not mapped | 9.0.6 / 2.16.4 |
| JWT lib | System.IdentityModel.Tokens.Jwt | 8.19.1 |
| Testing | xUnit + Moq + EF InMemory | 2.9.3 / 4.20.72 / 10.0.9 |

## Architecture

```
HatForge.API
  Controllers, SignalR Hub, Middleware, DI, DbSeederHostedService
    ↓ depends on
HatForge.Application
  Services, DTOs, Validators, Interfaces (INotificationPublisher, IUnitOfWork,
  IRepository<T>, IFileStorageService, IJwtTokenGenerator, IPasswordHasher)
    ↓ depends on
HatForge.Domain
  Entities, Enums, IRepository<T>, IUnitOfWork interfaces (no external packages)
    ↑ implemented by
HatForge.Infrastructure
  AppDbContext, EF entity configs, generic Repository<T>, UnitOfWork,
  JwtTokenGenerator, CloudinaryFileStorageService, PasswordHasher
    ↑ tested by
HatForge.Tests
  xUnit + Moq + EF InMemory
```

## Setup

```bash
# 1. Restore
dotnet restore HatForge.slnx

# 2. Create the database (PostgreSQL must be running on localhost:5432)
createdb hatforge_db

# 3. Override local settings in ignored src/HatForge.API/appsettings.Development.json
#    or via user secrets / environment variables:
#    ConnectionStrings:DefaultConnection, Jwt:Secret, Cloudinary:*

# 4. Run — DbSeederHostedService auto-applies EF migrations on startup
dotnet run --project src/HatForge.API
# HTTP:   http://localhost:5235
# HTTPS:  https://localhost:7150
# Swagger: http://localhost:5235/swagger

# 5. Tests
dotnet test
```

## Seed Data (auto-created on first run)

`DbSeederHostedService` runs migrations, then seeds 3 workshops, 1 hat model, and these accounts:

| Email | Password | Role | Workshop |
|---|---|---|---|
| admin@hatforge.com | `Admin123!` | Admin | — |
| lead@hatforge.com | `Lead123!` | Lead | — |
| staff@hatforge.com | `Staff123!` | Staff | Cutting |
| staff2@hatforge.com | `Staff123!` | Staff | Sewing |
| staff3@hatforge.com | `Staff123!` | Staff | Finishing |
| qc1@hatforge.com | `Qc123!` | QC Workshop | Cutting |
| qc2@hatforge.com | `Qc123!` | QC Workshop | Sewing |
| qc3@hatforge.com | `Qc123!` | QC Workshop | Finishing |
| transport@hatforge.com | `Transport123!` | QC Transport | — |
| gate@hatforge.com | `Gate123!` | QC Gate | — |

Workshops: `Cutting` (requires materials), `Sewing`, `Finishing`.

## API Surface

All non-auth endpoints require `Authorization: Bearer <token>`.
All responses use the envelope `{ success, data, error, errors }`.

| Endpoint | Role |
|---|---|
| `POST /api/auth/login` | anonymous |
| `GET /api/admin-dashboard` | Admin |
| `GET /api/hatmodel`; `POST /api/hatmodel`, `PUT /api/hatmodel/{id}`, `DELETE /api/hatmodel/{id}` | any / Admin |
| `GET /api/user`, `POST /api/user`, `DELETE /api/user/{id}` | Admin |
| `POST /api/batch` | Admin |
| `PUT /api/batch/{id}/plan` | Lead |
| `GET /api/batch/my`, `GET /api/batch/pending-gate-qc`, `GET /api/batch`, `GET /api/batch/{id}`, `GET /api/batch/{id}/final-summary` | role-specific |
| `PUT /api/batch/{id}/workshops/{workshopId}/complete` | QCGate, Lead |
| `PUT /api/batch/{id}/lead-approve` | Lead |
| `PUT /api/batch/{id}/gate-confirm` | QCGate |
| `POST /api/work` (multipart, photos required) | Staff |
| `PUT /api/work/approve`, `PUT /api/work/reject` | QC Workshop |
| `POST /api/transfer` (server resolves next workshop) | QC Workshop |
| `PUT /api/transfer/approve` | Lead |
| `PUT /api/transfer/confirm-receipt` (inspection quantities) | QC Workshop |
| `GET /api/transfer/pending`, `GET /api/transfer/awaiting-receipt` | Lead / QC Workshop |
| `GET /api/material/pending`, `PUT /api/material/confirm` | QC Workshop |
| `POST /api/material-request/ad-hoc` | QC Workshop |
| `PUT /api/material-request/{id}/approve`, `GET /api/material-request/pending` | Lead |
| `PUT /api/material-request/{id}/confirm`, `GET /api/material-request/batch/{batchId}` | QC Workshop / any |
| `/api/lead-task-delegation/*` | Lead / Admin / QCTransport |
| `GET /api/notification`, `GET /api/notification/unread-count`, `PUT /api/notification/{id}/read`, `PUT /api/notification/read-all` | any |

Full request/response shapes: see [`.claude/docs/api_endpoints.md`](.claude/docs/api_endpoints.md).

## Key Workflows

1. **Batch creation → planning** — Admin creates a batch, Lead plans the workshop chain (optionally
   scheduling material deliveries with `EstimatedMetersPerUnit`).
2. **Material delivery** — Receiving QC confirms actual quantities; if the workshop is first in chain
   and items were short, a top-up request is auto-created.
3. **Work submission** — Staff uploads photos and a quantity. Material is pre-charged
   (`quantity * EstimatedMetersPerUnit`).
4. **QC review** — QC approves (records `actualMaterialUsed`, reconciles the estimate) or rejects with a
   `passed / repairable / unrepairable` breakdown.
5. **Transfer** — QC of the source workshop creates a transfer; the server picks the next workshop by
   `OrderIndex`. Lead approves. Destination QC confirms receipt with usable + defective counts
   (`usable + defective` must equal the approved quantity). For the last workshop, transfer creation
   instead escalates the batch to `PendingLeadReview`.
6. **Lead final review → QC Gate** — Lead approves final, QCGate confirms and the batch is marked
   `Completed` with `CompletedQuantity` auto-computed from the last workshop's `PassedQuantity`.

## SignalR Hub

Connect at `/hubs/notifications` with JWT via query string:
```
?access_token=<jwt_token>
```

Client methods: `JoinBatch(int)`, `JoinWorkshop(int)`, `JoinAdmins()`, `JoinLeads()`, `JoinQCGate()`, `JoinUser(int)`.
Server pushes events like `WorkSubmitted`, `WorkApproved`, `WorkRejected`, `TransferRequested`,
`TransferApproved`, `WorkCanBegin`, `MaterialDeliveryConfirmed`, `MaterialShortfall`,
`MaterialRequestApproved`, `MaterialRequestFulfilled`, `AdHocMaterialRequest`, `MaterialLowAlert`,
`FinalReviewRequested`, `GateQCReviewRequested`, `BatchCompleted`, `LeadTaskDelegationRequested`,
`LeadTaskDelegationApproved`, `LeadTaskDelegationRejected`, `LeadTaskDelegationCompleted`. See
[`.claude/docs/realtime.md`](.claude/docs/realtime.md) for the full event/group table.

## Project Structure

```
src/
├── HatForge.Domain/          Entities, Enums, IRepository<T>, IUnitOfWork
├── HatForge.Application/     Services, DTOs, Validators, Interfaces, Common (exceptions, ApiResponse)
├── HatForge.Infrastructure/ AppDbContext, EF configs, Repository<T>, UnitOfWork,
│                             JwtTokenGenerator, CloudinaryFileStorageService, PasswordHasher
├── HatForge.API/             Controllers, SignalR Hub (NotificationHub), Middleware, DbSeeder
└── HatForge.Tests/           xUnit (Unit + Integration); NoOpNotificationPublisher in Fixtures/
```

## Documentation

| Topic | File |
|---|---|
| Agent onboarding | [`CLAUDE.md`](CLAUDE.md) |
| Architecture, layers, DI | [`.claude/docs/architecture.md`](.claude/docs/architecture.md) |
| Data models, enums, EF constraints | [`.claude/docs/data_models.md`](.claude/docs/data_models.md) |
| Batch state machine + workflows | [`.claude/docs/batch_workflow.md`](.claude/docs/batch_workflow.md) |
| Roles, JWT shape, authorization | [`.claude/docs/auth.md`](.claude/docs/auth.md) |
| API endpoints | [`.claude/docs/api_endpoints.md`](.claude/docs/api_endpoints.md) |
| SignalR hub + events | [`.claude/docs/realtime.md`](.claude/docs/realtime.md) |
| Test setup + fixtures | [`.claude/docs/testing.md`](.claude/docs/testing.md) |
