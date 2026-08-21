# CashBook Backend API & Services

[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-10.0-blue.svg)](https://learn.microsoft.com/en-us/ef/core/)
[![SQLite](https://img.shields.io/badge/SQLite-3.0-003B57.svg)](https://www.sqlite.org/)
[![Tests](https://img.shields.io/badge/Tests-127%20Passed-brightgreen.svg)]()

The backend engine for CashBook, built with **ASP.NET Core (.NET 10.0)** following Clean Architecture principles, Repository and Unit of Work patterns, and Entity Framework Core with SQLite.

---

## Table of Contents

- [Architecture & Solution Structure](#architecture--solution-structure)
- [Key Features](#key-features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [API Documentation (Swagger)](#api-documentation-swagger)
- [API Endpoints Reference](#api-endpoints-reference)
- [Database & EF Core Migrations](#database--ef-core-migrations)
- [Running Automated Tests](#running-automated-tests)
- [Configuration & Environment](#configuration--environment)
- [Publishing for Production](#publishing-for-production)

---

## Architecture & Solution Structure

The backend solution (`backend.slnx`) is organized into distinct layers for modularity, testability, and separation of concerns:

```text
backend/
├── backend/                  # Presentation / Web API Host Layer
│   ├── Controllers/          # RESTful API controllers (Accounts, Transactions, Descriptions, Database, Health)
│   ├── Extensions/           # Dependency Injection & middleware configuration extensions
│   ├── Middleware/           # Global exception handling & HTTP status code middleware
│   ├── Validators/           # FluentValidation request models & rules
│   ├── Program.cs            # Application startup & request pipeline configuration
│   └── appsettings.json      # Configuration settings & CORS definitions
├── backend.service/          # Application & Business Logic Layer
│   ├── Services/             # Domain services (AccountService, TransactionService, DescriptionService, DatabaseService)
│   ├── UnitOfWork/           # Unit of Work & generic Repository pattern implementation
│   └── Helpers/              # Excel import/export utilities powered by ClosedXML
├── backend.model/            # Domain & Data Layer
│   ├── DbModels/             # EF Core entities (Account, Transaction, Description, etc.) and AppDbContext
│   ├── RequestModels/        # Incoming API request DTOs & query filters
│   ├── ResponseModels/       # Outgoing standardized API response DTOs
│   └── Migrations/           # EF Core code-first migration snapshots
├── backend.common/           # Cross-Cutting Concerns
│   ├── AppConfiguration.cs   # Strongly-typed configuration bindings
│   ├── HttpStatusCodeException.cs # Custom exception types with HTTP status mapping
│   ├── QueryExtensions.cs    # Dynamic pagination, sorting, and IQueryable filtering extensions
│   └── FluentInterceptor.cs  # FluentValidation pipeline interceptor
└── backend.tests/            # Test Suite
    ├── Controllers/          # Controller unit tests (Moq + ActionResult validation)
    ├── Services/             # Service layer tests with SQLite in-memory databases
    ├── Repositories/         # Data access & Unit of Work tests
    ├── Validators/           # FluentValidation rule verification tests
    └── Migrations/           # Database migration integrity tests
```

---

## Key Features

- **RESTful Endpoints**: Versioned Web API controllers with standardized JSON responses.
- **Data Validation**: Request validation powered by FluentValidation before controller execution.
- **Repository & Unit of Work**: Transactional integrity across operations with decoupled data access.
- **Dynamic Querying**: Generic `QueryExtensions` supporting paginated grids, column-based sorting, and multi-field filtering.
- **Automated Database Migrations**: EF Core migrations automatically applied on application launch.
- **ClosedXML Excel Processing**: High-speed Excel export and import for transaction logs.
- **Comprehensive Test Suite**: 127+ unit and integration tests with xUnit, Moq, and SQLite in-memory fixtures.

---

## Prerequisites

- **[.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** or higher
- **dotnet-ef CLI tool** (optional, for generating migrations):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## Getting Started

### Running the API locally

From the project root:

```bash
npm run dev:backend
```

Or from the `backend/backend` folder:

```bash
dotnet run --urls http://localhost:5050
```

The API will be available at `http://localhost:5050`.

---

## API Documentation (Swagger)

When running in `Development` mode, Swagger UI is automatically hosted at:

```
http://localhost:5050/swagger
```

---

## API Endpoints Reference

### Health Check

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | Application & database connectivity health check |

### Accounts (`/api/v1/accounts`)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/accounts` | Retrieve all accounts with balance summaries |
| `GET` | `/api/v1/accounts/{id}` | Retrieve single account by ID |
| `POST` | `/api/v1/accounts` | Create a new account |
| `PUT` | `/api/v1/accounts/{id}` | Update existing account details |
| `DELETE` | `/api/v1/accounts/{id}` | Delete an account |

### Transactions (`/api/v1/transactions`)

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/v1/transactions/list` | Paginated & filtered transaction list |
| `GET` | `/api/v1/transactions/{id}` | Retrieve transaction by ID |
| `POST` | `/api/v1/transactions` | Record a new transaction (Income, Expense, Transfer) |
| `PUT` | `/api/v1/transactions/{id}` | Update an existing transaction |
| `DELETE` | `/api/v1/transactions/{id}` | Delete a transaction |
| `GET` | `/api/v1/transactions/export` | Export filtered transactions to Excel (`.xlsx`) |
| `POST` | `/api/v1/transactions/import` | Import transactions from Excel file |

### Descriptions (`/api/v1/descriptions`)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/descriptions` | Retrieve all stored descriptions |
| `GET` | `/api/v1/descriptions/search` | Search descriptions for autocomplete dropdown |
| `POST` | `/api/v1/descriptions` | Add a new description suggestion |
| `PUT` | `/api/v1/descriptions/{id}` | Update an existing description |
| `DELETE` | `/api/v1/descriptions/{id}` | Delete a description |

### Database Management (`/api/v1/database`)

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/v1/database/backup` | Create a timestamped database backup |
| `POST` | `/api/v1/database/restore` | Restore database from a specific backup file |
| `POST` | `/api/v1/database/seed` | Seed initial demo data |
| `GET` | `/api/v1/database/stats` | Retrieve database size, records count, and backup list |

---

## Database & EF Core Migrations

The database is an embedded **SQLite** file (`cashbook.db`).

### Automatic Migrations

Migrations are automatically applied on API startup via:
```csharp
DatabaseServiceExtension.ApplyMigrations(app);
```

### Creating a New Migration

To add a new EF Core migration when modifying entity models in `backend.model`:

```bash
# Run from the 'backend' directory:
dotnet ef migrations add <MigrationName> --project backend.model --startup-project backend
```

### Applying Migrations Manually

```bash
dotnet ef database update --project backend.model --startup-project backend
```

---

## Running Automated Tests

The solution includes an extensive xUnit test suite covering all layers:

```bash
# Run tests across all projects:
dotnet test backend/backend.slnx

# Run tests with detailed verbosity:
dotnet test backend/backend.slnx --logger "console;verbosity=normal"
```

---

## Configuration & Environment

Configuration is loaded from `appsettings.json` and `appsettings.Development.json`.

### Environment Variables

| Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Environment name (`Development`, `Production`) |
| `DATABASE_PATH` | Optional absolute path override for `cashbook.db` (used in portable mode) |

---

## Publishing for Production

To publish a self-contained single-file executable for the Electron package:

### Windows (x64)

```bash
dotnet publish backend/backend.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output ../electron/resources/api
```

### macOS (ARM64 / x64)

```bash
dotnet publish backend/backend.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output ../electron/resources/api
```

---

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.