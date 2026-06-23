# HatForge — Agent Onboarding Guide

## Project Overview

HatForge is a real-time manufacturing batch orchestration API for Vietnamese hat production facilities.
It digitizes multi-stage workshop workflows that were previously paper-based: tracking production batches
through an ordered chain of workshops, with role-gated approvals, photo-evidence QC, workshop transfers,
material delivery scheduling, and real-time push notifications at each stage.

---

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET | 10.0 |
| Web framework | ASP.NET Core (Web API) | 10.0 |
| ORM | Entity Framework Core | 10.0.9 |
| Database | PostgreSQL (via Npgsql) | 10.0.2 |
| Real-time | SignalR (built-in ASP.NET Core) | — |
| Authentication | JWT Bearer (HS256, 7-day expiry) | 10.0.9 |
| Password hashing | BCrypt.Net-Next | 4.2.0 |
| Validation | FluentValidation | 12.1.1 |
| File storage | Cloudinary | 1.27.1 |
| API docs | Swashbuckle + Scalar | 9.0.6 / 2.16.4 |
| Testing | xUnit + Moq + EF InMemory | 2.9.3 / 4.20.72 |

---

## Dev Commands

```bash
# Restore dependencies
dotnet restore HatForge.slnx

# Run the API (migrations + seed run automatically on startup)
dotnet run --project src/HatForge.API
# HTTP:  http://localhost:5235
# HTTPS: https://localhost:7150
# Swagger: http://localhost:5235/swagger

# Run tests (single pass, no watch mode)
dotnet test

# Apply migrations manually (only if needed)
dotnet ef database update -p src/HatForge.Infrastructure -s src/HatForge.API
```

Prerequisites: PostgreSQL running at `localhost:5432`, database `hatforge_db`.
Cloudinary credentials required for photo uploads — already pre-configured in `appsettings.Development.json`.

---

## Core Logic Summary

A `Batch` moves through an ordered chain of `Workshop`s defined during planning.
Each workshop processes work (Staff submits → QCWorkshop approves → QCWorkshop initiates transfer → Lead approves → destination QC confirms receipt).
The server auto-resolves the next workshop by `OrderIndex` — callers never specify a target workshop on transfer creation.
When the last workshop completes, the batch escalates to Lead final review, then QC Gate confirmation.

> Full batch state machine and workflow: see [`.claude/docs/batch_workflow.md`](.claude/docs/batch_workflow.md)

---

## Key Constraints

- **Do not bypass the `INotificationPublisher` abstraction.** Services must never reference `SignalRNotificationPublisher` directly. Tests use `NoOpNotificationPublisher`.
- **Do not add AutoMapper.** All DTO mapping is manual via private static methods inside each service.
- **Do not add MediatR or CQRS.** Services are called directly from controllers — this is intentional.
- **Do not use `DbContext` directly in services.** All data access goes through `IUnitOfWork` and `IRepository<T>`.
- **Do not change `BatchStatus` enum integer values.** They are persisted to the DB. Current values have an intentional gap (see [`.claude/docs/architecture.md`](.claude/docs/architecture.md)).
- **JWT claims shape is fixed.** `workshopId` is a custom claim read by `BaseApiController.CurrentWorkshopId` — changing it breaks all role-gated endpoints.
- **All API responses must use the `ApiResponse<T>` envelope.** Never return raw objects from controllers.
- **Migrations run automatically at startup** via `DbSeederHostedService`. Do not disable this behavior.
- **Photo file uploads** accept only `.jpg`, `.jpeg`, `.png`, `.webp`. Max URL length: 512 chars.

---

## Additional Documentation

| Topic | File |
|---|---|
| Clean Architecture layers, patterns, DI wiring | [`.claude/docs/architecture.md`](.claude/docs/architecture.md) |
| Domain entities, relationships, EF constraints | [`.claude/docs/data_models.md`](.claude/docs/data_models.md) |
| Batch state machine, full end-to-end workflow | [`.claude/docs/batch_workflow.md`](.claude/docs/batch_workflow.md) |
| Roles, JWT structure, authorization rules | [`.claude/docs/auth.md`](.claude/docs/auth.md) |
| SignalR hub groups, real-time events | [`.claude/docs/realtime.md`](.claude/docs/realtime.md) |
| API endpoints, request/response shapes | [`.claude/docs/api_endpoints.md`](.claude/docs/api_endpoints.md) |
| Test setup, fixtures, seed accounts | [`.claude/docs/testing.md`](.claude/docs/testing.md) |
