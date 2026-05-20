# Task Management API

A scalable **Project & Task Management REST API** built with **.NET 9**, **Clean Architecture**, **CQRS + MediatR**, **Redis caching**, **JWT Authentication**, **API Versioning**, and full **Docker** support.

---

## Architecture Overview

```
TaskManagement/
├── src/
│   ├── TaskManagement.Domain/          # Entities, Enums, Base classes (no dependencies)
│   ├── TaskManagement.Application/     # CQRS Commands/Queries, Interfaces, Validators, DTOs
│   ├── TaskManagement.Infrastructure/  # EF Core, JWT, BCrypt, Redis, Service implementations
│   └── TaskManagement.API/             # Controllers (v1), Middleware, Swagger, Program.cs
└── tests/
    └── TaskManagement.Tests/           # xUnit unit tests with Moq + FluentAssertions
```

### Key Patterns & Bonus Features Implemented

| Feature | Details |
|---|---|
| **Clean Architecture** | 4-layer separation: Domain → Application → Infrastructure → API |
| **CQRS + MediatR** | Every operation is a Command or Query with its own Handler |
| **Dependency Injection** | All layers registered via extension methods |
| **SOLID Principles** | Single responsibility per handler, open/closed via interfaces |
| **DTO Usage** | All API responses use typed DTOs, never domain entities |
| **Global Exception Handling** | Middleware maps exceptions to RFC-compliant HTTP responses |
| **FluentValidation** | Validators for every command, run through MediatR pipeline behavior |
| **Generic Response Wrapper** | `ApiResponse<T>` wraps all responses with success/message/errors |
| **Pagination** | `PagedResult<T>` on all list endpoints |
| **JWT Authentication** | HS256 tokens, configurable expiry |
| **Role-based Authorization** | `Admin` / `User` roles, policy-based |
| **API Versioning** | URL segment versioning (`/api/v1/...`), header versioning supported |
| **Redis Caching** | Projects and tasks cached, invalidated on mutations |
| **Docker** | Dockerfile + docker-compose with SQL Server + Redis |
| **EF Core Migrations** | Migration files included, auto-applied on startup |

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- SQL Server (local or Docker)
- Redis (optional — falls back to in-memory if not configured)
- Docker + Docker Compose (for containerized setup)

---

## Quick Start — Local

### 1. Configure the connection string

Edit `src/TaskManagement.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TaskManagementDb;Trusted_Connection=True;TrustServerCertificate=True;",
    "Redis": ""
  },
  "JwtSettings": {
    "SecretKey": "YourSecretKeyMustBe32CharsOrMore!!",
    "Issuer": "TaskManagementAPI",
    "Audience": "TaskManagementClient",
    "ExpiryMinutes": "1440"
  }
}
```

> Leave `Redis` empty to use in-memory caching instead.

### 2. Run the API

```bash
cd src/TaskManagement.API
dotnet run
```

The API starts at `http://localhost:5000`. Swagger UI is available at the root: `http://localhost:5000`.

> Database migrations run **automatically** on startup.

---

## Quick Start — Docker

```bash
docker-compose up --build
```

This starts:
- **API** on `http://localhost:5000`
- **SQL Server** on `localhost:1433`
- **Redis** on `localhost:6379`

---



## API Reference

### Authentication

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/v1/auth/register` | Register a new user |
| POST | `/api/v1/auth/login` | Login and receive JWT |

### Projects (requires `Authorization: Bearer <token>`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/projects` | Get all projects (paginated) |
| GET | `/api/v1/projects/{id}` | Get project by ID |
| POST | `/api/v1/projects` | Create a project |
| PUT | `/api/v1/projects/{id}` | Update a project |
| DELETE | `/api/v1/projects/{id}` | Delete a project |

### Tasks (requires `Authorization: Bearer <token>`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/projects/{projectId}/tasks` | Get tasks for a project (paginated, filterable by `?status=`) |
| POST | `/api/v1/projects/{projectId}/tasks` | Create a task |
| PUT | `/api/v1/projects/{projectId}/tasks/{id}` | Update full task |
| PATCH | `/api/v1/projects/{projectId}/tasks/{id}/status` | Update task status only |
| DELETE | `/api/v1/projects/{projectId}/tasks/{id}` | Delete a task |

### Enum Values

**Status:** `0=Todo`, `1=InProgress`, `2=Done`, `3=Cancelled`

**Priority:** `0=Low`, `1=Medium`, `2=High`, `3=Critical`

---

## Postman Collection

Import `TaskManagement.postman_collection.json` into Postman.

The collection auto-saves the JWT token after login and injects it into all subsequent requests via collection variables.

---

## Database Migrations

Migration files are in:
```
src/TaskManagement.Infrastructure/Data/Migrations/
```

To add a new migration manually:
```bash
dotnet ef migrations add <MigrationName> \
  --project src/TaskManagement.Infrastructure \
  --startup-project src/TaskManagement.API
```

To apply migrations manually:
```bash
dotnet ef database update \
  --project src/TaskManagement.Infrastructure \
  --startup-project src/TaskManagement.API
```

---

## Project Structure (Detailed)

```
src/TaskManagement.Domain/
├── Common/BaseEntity.cs              # Id, CreatedAt, UpdatedAt
├── Entities/User.cs
├── Entities/Project.cs
├── Entities/ProjectTask.cs
└── Enums/Enums.cs                    # TaskStatus, TaskPriority, UserRole

src/TaskManagement.Application/
├── Common/
│   ├── Behaviors/PipelineBehaviors.cs    # Validation + Logging MediatR pipeline
│   ├── Exceptions/Exceptions.cs          # NotFoundException, ValidationException, etc.
│   ├── Interfaces/IInterfaces.cs         # IApplicationDbContext, ITokenService, etc.
│   ├── Mappings/MappingProfile.cs        # AutoMapper profiles
│   └── Models/ApiResponse.cs             # Generic response wrapper + PagedResult
├── Features/
│   ├── Auth/Commands/AuthCommands.cs     # Register + Login handlers + validators
│   ├── Projects/Commands/               # Create, Update, Delete
│   ├── Projects/Queries/               # GetAll, GetById
│   ├── Tasks/Commands/                 # Create, UpdateStatus, Update, Delete
│   └── Tasks/Queries/                  # GetByProject
└── DependencyInjection.cs

src/TaskManagement.Infrastructure/
├── Cache/RedisCacheService.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── Configurations/EntityConfigurations.cs
│   └── Migrations/
├── Services/
│   ├── TokenService.cs
│   └── UserServices.cs               # PasswordService + CurrentUserService
└── DependencyInjection.cs

src/TaskManagement.API/
├── Controllers/V1/
│   ├── AuthController.cs
│   ├── ProjectsController.cs
│   └── TasksController.cs
├── Extensions/ApiVersioningExtensions.cs
├── Middleware/GlobalExceptionHandlingMiddleware.cs
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```
