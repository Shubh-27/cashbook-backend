# Cashbook Backend API

The core logic and database management services for the Cashbook application, built with ASP.NET Core and C#.

## Features

- **RESTful API**: Managed through ASP.NET Core controllers.
- **SQLite Database**: Local data storage for transactions, accounts, and categories.
- **Service Layer Architecture**: Separated into Domain Models, Business Logic, and Common Utilities.

## Project Structure

- `backend/`: The main API project containing controllers, program entry point, and database configuration.
- `backend.service/`: Business logic and repository layers for handling transactions and reports.
- `backend.model/`: Domain entities and data transfer objects (DTOs).
- `backend.common/`: Shared utilities and extensions used across the backend.

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the API

From the root project:

```bash
npm run dev:backend
```

Or from the `backend/backend` directory:

```bash
dotnet run --urls http://localhost:5050
```

The API is accessible at `http://localhost:5050`.

## Database

The application uses an SQLite database (`cashbook.db`) located in the `backend/backend` directory during development.

### Migrations

[Add instructions for handling migrations if applicable]

## Configuration

Settings are managed via `appsettings.json` and `Properties/launchSettings.json`.

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.