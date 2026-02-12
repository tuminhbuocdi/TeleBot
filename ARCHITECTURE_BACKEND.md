# TeleBot Management - Backend Architecture (Management.Api)

## Scope

This document describes the current backend structure and the implemented authentication/session flow.

## Solution / Projects

Repository root: `d:\CSharpProject\GitHub\TeleBot`

### `Management.Api` (ASP.NET Core Web API, .NET 8)

- **Role**: HTTP API entrypoint + Swagger + JWT auth + SignalR hub.
- **Key folders/files**
  - `Program.cs`: DI + middleware pipeline + JWT bearer validation + CORS.
  - `Controllers/AuthController.cs`: Register/Login endpoints.
  - `Hubs/AppHub.cs`: SignalR hub (broadcast).
  - `Extensions/ServiceExtensions.cs`: DI registrations.
  - `appsettings.json`: DB connection string + JWT settings.
  - `Properties/launchSettings.json`: dev ports.

### `Management.Application` (Application layer)

- **Role**: shared application services (JWT generation, password hashing) + DTOs.
- **Key files**
  - `Auth/JwtService.cs`: JWT creation.
  - `Auth/PasswordHasher.cs`: PBKDF2 password hashing.
  - `Auth/LoginRequest.cs`, `Auth/RegisterRequest.cs`, `Auth/AuthResponse.cs`: DTOs.

### `Management.Domain` (Domain layer)

- **Role**: entities.
- **Key files**
  - `Entities/User.cs`: maps to SQL table `Users`.

### `Management.Infrastructure` (Infrastructure layer)

- **Role**: DB access (SQL Server + Dapper) and repositories.
- **Key files**
  - `Db/DbConnectionFactory.cs`: creates `SqlConnection` from connection string `Default`.
  - `Db/UnitOfWork.cs`: transaction wrapper (currently registered but not used by auth flow).
  - `Repositories/UserRepository.cs`: queries/inserts/updates `Users`.

### `Management.Worker` (Worker Service)

- **Role**: background jobs (currently skeleton).

## Runtime Ports

From `Management.Api/Properties/launchSettings.json`:

- HTTPS: `https://localhost:7179`
- HTTP: `http://localhost:5295`

Frontend should call: `https://localhost:7179/api` in development.

## Dependency Flow (current)

- `Management.Api` references: `Management.Application`, `Management.Domain`, `Management.Infrastructure`.
- `Management.Application` references: `Management.Domain`.
- `Management.Infrastructure` references: `Management.Domain`.

Note: there is also a reference to `..\Managerment.Domain\Management.Domain.csproj` which appears missing and may produce build warnings.

## Database

### Connection string

In `Management.Api/appsettings.json`:

- `ConnectionStrings:Default` points to SQL Server.

### Table: `Users`

Entity: `Management.Domain.Entities.User`

Important columns used in current auth flow:

- Identity: `UserId UNIQUEIDENTIFIER (PK)`
- Login identifiers: `Username` (required), `Email` (nullable), `Phone` (nullable)
- Auth: `PasswordHash`
- Status: `IsActive`, `IsLocked`, `IsDeleted`
- Security controls: `FailedLoginCount`, `LockoutEnd`
- Audit: `LastLoginAt`, `LastLoginIp`, `CreatedAt`, `UpdatedAt`

### Nullable UNIQUE constraints on Email/Phone

If the DB uses UNIQUE constraints on nullable columns (`Email`, `Phone`), inserting multiple users with `NULL` can fail depending on how constraints were created.

Recommended approach is **filtered unique indexes**:

- Unique email only when not null
- Unique phone only when not null

## Authentication

### Password hashing

Implemented in `Management.Application/Auth/PasswordHasher.cs`:

- Algorithm: PBKDF2 (SHA256)
- Iterations: `100_000`
- Salt size: `16` bytes (random per hash)
- Key size: `32` bytes
- Stored format: `iterations.salt.key` (Base64 parts)

Each user has a distinct salt (generated for each `Hash(...)` call).

### JWT generation

Implemented in `Management.Application/Auth/JwtService.cs`:

- Signing: `HS256` using `Jwt:Key`.
- Minimum key length: **>= 32 bytes** (validated).
- Token claims include:
  - `sub`: userId
  - `unique_name`: username
  - `uid`, `uname`
  - `ClaimTypes.NameIdentifier`, `ClaimTypes.Name`, `ClaimTypes.Role`

#### Session lifetime

- JWT expires after **24 hours**: `DateTime.UtcNow.AddHours(24)`.
- JWT bearer validation uses `ClockSkew = TimeSpan.Zero` for strict expiry.

### Endpoints

Controller: `Management.Api/Controllers/AuthController.cs`

- `POST /api/auth/register`
  - Validates required fields: `Username`, `Password`
  - Checks duplicates by username/email/phone
  - Hashes password via `PasswordHasher`
  - Inserts user
  - Returns `{ token }`

- `POST /api/auth/login`
  - Allows login using username/email/phone
  - Verifies password
  - Lockout logic:
    - threshold: 5 failed attempts
    - duration: 15 minutes (`LockoutEnd`)
  - On success:
    - resets failed count
    - clears lockout
    - sets `LastLoginAt`, `LastLoginIp`
  - Returns `{ token }`

## CORS

In `Management.Api/Program.cs`:

- CORS policy name: `frontend`
- Allowed origins:
  - `http://localhost:5173`
  - `https://localhost:5173`

## DI Registrations

`Management.Api/Extensions/ServiceExtensions.cs` registers:

- `DbConnectionFactory` (Singleton)
- `UnitOfWork` (Scoped)
- `JwtService` (Scoped)
- `PasswordHasher` (Singleton)
- `UserRepository` (Scoped)

## Known Issues / TODO

- `Management.Api/Program.cs` has a duplicate `using Management.Api.Extensions;` (warning only).
- Project reference to `Managerment.Domain` appears missing.
- Consider moving auth logic into an application-level `AuthService` to keep controllers thin.
