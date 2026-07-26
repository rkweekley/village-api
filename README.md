# Village API

The backend API for the Village family productivity app — a .NET 10 ASP.NET Core service with Carter modules, JWT authentication, SignalR real-time hubs, and SQLite/PostgreSQL support.

## Tech Stack

- **Framework:** .NET 10, ASP.NET Core
- **API Layer:** Carter (minimal API modules)
- **ORM:** Entity Framework Core 10 with Npgsql (PostgreSQL) or SQLite
- **Auth:** JWT Bearer tokens with BCrypt password hashing
- **Real-Time:** SignalR with in-memory backplane (Redis ready)
- **Validation:** FluentValidation
- **Docs:** Scalar (OpenAPI UI)
- **Rate Limiting:** ASP.NET Core built-in rate limiter

## Features

| Module | Endpoints | Description |
|---|---|---|
| **Auth** | `POST /api/auth/register`, `POST /api/auth/login` | Registration and JWT token issuance |
| **Family** | `GET /api/family`, `POST /api/family/invite`, etc. | Family management and invite codes |
| **Chores** | `GET /api/chores`, `POST /api/chores`, `PUT /api/chores/{id}` | Chore CRUD with assignment workflow |
| **Calendar** | `GET /api/calendar/events`, `POST /api/calendar/events` | Event management |
| **Shopping** | `GET /api/shopping/lists`, `POST /api/shopping/lists`, `POST /api/shopping/lists/{id}/items` | Shopping lists with item management |
| **Meals** | `GET /api/mealplans`, `POST /api/mealplans`, plus recipes | Meal plans and recipe storage |
| **School** | `GET /api/school/assignments`, `POST /api/school/subjects` | Schoolwork tracking |
| **Rewards** | `GET /api/rewards`, `POST /api/rewards` | Reward shop with redemptions |
| **Recipes** | `GET /api/recipes`, `POST /api/recipes` | Recipe management |

### SignalR Hubs

| Hub | Path | Description |
|---|---|---|
| FamilyHub | `/hubs/family` | Family data push |
| ChoreHub | `/hubs/chores` | Chore status updates |
| PointsHub | `/hubs/points` | Real-time point updates |
| NotificationsHub | `/hubs/notifications` | In-app notifications |
| SchoolHub | `/hubs/school` | Schoolwork sync |
| MealPlanHub | `/hubs/mealplan` | Meal plan changes |

## Getting Started

### Prerequisites

- .NET SDK 10.0
- A PostgreSQL instance (or use SQLite for development)

### Setup

```bash
cd src/Village.Api
dotnet restore
dotnet run
```

The API starts on `http://localhost:5279` by default. The Scalar API reference is available at `/scalar/v1`.

### Configuration

Edit `appsettings.json` or use environment variables:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=village.db",        // SQLite (dev)
    "Redis": "localhost:6379"                   // Optional: Redis for SignalR backplane
  },
  "Jwt": {
    "Secret": "your-256-bit-secret-here",
    "Issuer": "village.app",
    "Audience": "village.app"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173", "http://localhost:3000"]
  }
}
```

For PostgreSQL, change the connection string:
```json
"Default": "Host=localhost;Database=village;Username=app;Password=password"
```

## Project Structure

```
src/
├── Village.Api/              # Web API project (Program.cs, modules, hubs)
│   ├── Dtos/                 # Request/response DTOs
│   ├── Extensions/           # Service registration extensions
│   ├── Hubs/                 # SignalR hub definitions
│   ├── Modules/              # Carter endpoint modules
│   └── Services/             # API-level services (JWT, Notifications)
├── Village.Application/      # Application logic, handlers
├── Village.Domain/           # Domain entities and enums
├── Village.Infrastructure/   # Data access (EF Core DbContext, migrations)
└── Village.Shared/           # Shared utilities
```

## API Documentation

When running in development mode, the Scalar API reference is available at:

```
http://localhost:5279/scalar/v1
```

The root endpoint (`GET /`) redirects there automatically.

## Migrations

Migrations run automatically on startup in development mode (`app.Environment.IsDevelopment()`). To manually apply:

```bash
dotnet ef database update
```

## Rate Limiting

- **Auth endpoints:** 10 requests per minute (3 queue slots)
- **Invite lookup:** 20 requests per minute (no queue)

Both return `429 Too Many Requests` when exceeded.
