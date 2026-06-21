# HatForge

Real-time manufacturing batch orchestration system for Vietnamese hat manufacturers.

## Problem

Hat manufacturers lack digital systems to manage multi-stage production workflows. Currently managed on paper with no real-time visibility, manual workshop coordination, or quality control audit trails.

## Solution

A web platform that digitizes multi-stage manufacturing workflows with real-time production tracking, multi-role collaboration, and quality control approval gates.

## Features

- **Batch tracking** through multiple production stages with 5 core roles
- **Staff submission** with photo evidence
- **QC approval/rejection workflow** with detailed rejection reasons
- **Real-time notifications** via SignalR WebSocket
- **Role-based access control** (Admin, Lead, Staff, QC Workshop, QC Gate)
- **Workshop transfer requests** between production stages
- **Material delivery scheduling** and confirmation
- **Clean Architecture** design (Domain → Application → Infrastructure → API)

## Tech Stack

ASP.NET Core 8 | Entity Framework Core | PostgreSQL | SignalR | JWT Authentication | xUnit

## Architecture

```
API (Controllers, Hubs, Middleware)
    ↓
Application (Services, DTOs, Validators, Interfaces)
    ↓
Domain (Entities, Enums, Repository Interfaces)
    ↓
Infrastructure (EF Core, Repositories, FileStorage, JWT)
```

## Setup

```bash
# 1. Clone and restore
dotnet restore HatForge.slnx

# 2. Setup PostgreSQL
createdb hatforge_db

# 3. Update connection string in appsettings.json if needed

# 4. Run migrations (or let the app auto-migrate on startup)
dotnet ef database update -p src/HatForge.Infrastructure -s src/HatForge.API

# 5. Run
dotnet run --project src/HatForge.API

# 6. Run tests
dotnet test
```

## Seed Data (auto-created on first run)

| Email | Password | Role |
|-------|----------|------|
| admin@hatforge.com | Admin123! | Admin |
| lead@hatforge.com | Lead123! | Lead |
| staff@hatforge.com | Staff123! | Staff |
| qc@hatforge.com | Qc123! | QC Workshop |
| gate@hatforge.com | Gate123! | QC Gate |

## API Documentation

Swagger UI: `http://localhost:5000/swagger` (Development mode)

## Key Workflows

### 1. Batch Creation
`POST /api/batch` (Admin) → Creates batch, assigns Lead, selects workshop chain

### 2. Work Submission
`POST /api/work` (Staff) → Submit photo + quantity for QC review

### 3. QC Review
`PUT /api/work/approve` / `PUT /api/work/reject` (QC Workshop) → Approve or reject with reason

### 4. Transfer
`POST /api/transfer` (Lead) → Request transfer to next workshop
`PUT /api/transfer/approve` (Lead) → Confirm and move batch

### 5. Material Delivery
`POST /api/material/schedule` (Lead) → Schedule materials
`PUT /api/material/confirm` (QC Workshop) → Confirm receipt

## SignalR Hub

Connect at `/hubs/notifications` with JWT token via query string:
```
?access_token=<jwt_token>
```

Hub groups: `batch_{id}`, `workshop_{id}`, `admins`, `leads`, `user_{id}`

## Project Structure

```
src/
├── HatForge.Domain/          Entities, Enums, Interfaces
├── HatForge.Application/     Services, DTOs, Validators
├── HatForge.Infrastructure/ EF Core, Repositories, External Services
├── HatForge.API/             Controllers, SignalR Hub, Middleware
└── HatForge.Tests/           xUnit tests (24 tests, 0 failures)
```
