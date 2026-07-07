# EMS — Enterprise Management System

A modular mini-ERP (employees, assets, inventory, fleet, procurement, maintenance, leave)
built as a production-quality showcase of enterprise .NET engineering.

**Stack:** ASP.NET Core Web API (.NET 10) · Blazor WebAssembly · SQL Server + EF Core ·
Clean Architecture · JWT + permission-based RBAC · Azure · Azure DevOps.

## Solution layout

```
src/
  EMS.Domain           Entities and business rules (no external dependencies)
  EMS.Application      Use cases (MediatR), validation, abstractions
  EMS.Infrastructure   EF Core, Identity, storage, email, reports
  EMS.WebAPI           REST API + composition root; serves the Blazor client
  EMS.BlazorUI         Blazor WebAssembly front end
  EMS.Shared           Contracts shared between API and client
tests/
  EMS.Architecture.Tests   Layer-dependency rules (NetArchTest)
  EMS.Domain.Tests / EMS.Application.Tests / EMS.IntegrationTests / EMS.UI.Tests
```

Layer boundaries are enforced by tests — see `tests/EMS.Architecture.Tests`.

## Run locally

```bash
dotnet run --project src/EMS.WebAPI
```

Then open the printed URL — the API serves the Blazor UI. Health check at `/health`,
OpenAPI at `/openapi/v1.json` (Development). On first run the database is migrated and
seeded automatically (LocalDB, Development only).

**Demo accounts** (dev seed): `admin@ems.local` / `Admin123!` (Super Admin) ·
`employee@ems.local` / `Employee123!` (Employee).

Auth: 15-minute JWTs carrying permission claims + rotating one-time refresh tokens in an
HttpOnly cookie; replaying a consumed refresh token revokes the whole token family.

## Documentation

- [System Design Document](docs/SYSTEM_DESIGN.md) — architecture, module designs, data model, security, Azure/CI-CD plans.
