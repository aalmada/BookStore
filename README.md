# Book Store

[![CI](https://github.com/aalmada/BookStore/actions/workflows/ci.yml/badge.svg)](https://github.com/aalmada/BookStore/actions/workflows/ci.yml)
[![CodeQL](https://github.com/aalmada/BookStore/actions/workflows/codeql.yml/badge.svg)](https://github.com/aalmada/BookStore/actions/workflows/codeql.yml)

Full-stack online book store application with event-sourced backend API and Blazor frontend, orchestrated by .NET Aspire.

## Overview

A complete book store management system featuring:
- **Backend API**: Event-sourced ASP.NET Core Minimal APIs with Marten and PostgreSQL
- **Frontend**: Blazor web application for browsing and managing books
- **Orchestration**: .NET Aspire for local development, deployment, and observability
- **Database**: PostgreSQL with event store and read model projections
- **Modern Stack**: .NET 10 with C# 14 (latest language features)

## 🚀 Quick Start

```bash
# Prerequisites: .NET 10 SDK, Aspire CLI, Docker Desktop

# Install Aspire CLI: Follow instructions at https://aspire.dev/get-started/install-cli/

# Clone and run
git clone <repository-url>
cd BookStore
dotnet restore
aspire run
```

The Aspire dashboard opens automatically, providing access to:
- **Web Frontend** - Blazor application for browsing books
- **API Service** - Backend API with Scalar documentation at `/scalar/v1`
- **PostgreSQL** - Event store and read model database
- **PgAdmin** - Database management interface

## ✨ Features

### Frontend (Blazor Web)
- **Book Catalog** with search and filtering
- **Book Details** with comprehensive information
- **Real-time Updates** with SignalR notifications
- **Optimistic UI** for instant feedback with eventual consistency
- **Responsive Design** for desktop and mobile
- **Type-safe API Client** with BookStore.Client library (Refit-based)
- **Resilience** with Polly (retry and circuit breaker)

### Backend API
- **Event Sourcing** with Marten and PostgreSQL
- **CQRS** with async projections for optimized reads
- **Real-time Notifications** with SignalR (Wolverine integration)
- **Multi-language Support** for categories (en, pt, es, fr, de)
- **Full-text Search** with PostgreSQL trigrams and unaccent
- **Optimistic Concurrency** with ETags
- **Distributed Tracing** with correlation/causation IDs
- **API Versioning** (header-based, v1.0)
- **Soft Deletion** - Logical deletes with restore capability

## Architecture Enforcement

The project includes a custom **Roslyn Analyzer** (`BookStore.ApiService.Analyzers`) that enforces Event Sourcing, CQRS, and DDD patterns:

- ✅ Events must be immutable record types
- ✅ Commands follow CQRS conventions
- ✅ Aggregates use proper Marten Apply methods
- ✅ Handlers follow Wolverine conventions
- ✅ Consistent namespace organization

See [Analyzer Rules Documentation](docs/analyzer-rules.md) for details.

- **Native OpenAPI** with Scalar UI
- **Structured Logging** with correlation IDs

### Infrastructure (.NET Aspire)
- **Service Orchestration** for local development
- **Service Discovery** between frontend and backend
- **OpenTelemetry** integration for observability
- **Container Management** for PostgreSQL and PgAdmin
- **Dashboard** for monitoring all services

## 📁 Project Structure

```
BookStore/
├── src/
│   ├── ApiService/
│   │   ├── BookStore.ApiService/      # Backend API with event sourcing
│   │   │   ├── Aggregates/            # Domain aggregates
│   │   │   ├── Events/                # Domain events
│   │   │   ├── Commands/              # Command definitions
│   │   │   ├── Handlers/              # Wolverine command handlers
│   │   │   ├── Projections/           # Read model projections
│   │   │   ├── Endpoints/             # API endpoints
│   │   │   └── Infrastructure/        # Cross-cutting concerns
│   │   │
│   │   └── BookStore.ApiService.Analyzers/  # Roslyn analyzers
│   │
│   ├── Client/
│   │   └── BookStore.Client/          # Reusable API client library
│   │       ├── IBookStoreApi.cs       # Refit interface
│   │       ├── BookStoreClientExtensions.cs  # DI helpers
│   │       └── README.md              # Usage guide
│   │
│   ├── Web/
│   │   └── BookStore.Web/             # Blazor frontend
│   │       ├── Components/            # Blazor components
│   │       └── Services/              # Application services
│   │
│   ├── Shared/
│   │   ├── BookStore.Shared/          # Shared domain models & DTOs
│   │   └── BookStore.Shared.Tests/    # Unit tests for shared code
│   │
│   ├── BookStore.AppHost/             # Aspire orchestration
│   │   └── Program.cs                 # Service configuration
│   │
│   └── BookStore.ServiceDefaults/     # Shared service configuration
│       └── Extensions.cs              # OpenTelemetry, health checks
│
├── docs/                              # Documentation
│   ├── getting-started.md             # Setup guide
│   ├── architecture.md                # System design
│   ├── api-client-generation.md       # Client library usage
│   ├── wolverine-guide.md             # Command/handler pattern
│   └── ...
│
├── _tools/                            # Development tools
│   └── update-openapi.sh              # OpenAPI spec updater
│
├── BookStore.slnx                     # Solution file (new .slnx format)
└── README.md                          # This file
```

## 📖 Documentation

- **[Getting Started](docs/getting-started.md)** - Setup and first steps
- **[Architecture Overview](docs/architecture.md)** - System design and patterns
- **[Aspire Orchestration Guide](docs/aspire-guide.md)** - Service orchestration and local development
- **[Aspire Deployment Guide](docs/aspire-deployment-guide.md)** - Deploy to Azure and Kubernetes
- **[Production Scaling Guide](docs/production-scaling-guide.md)** - Scale applications and databases in production
- **[Configuration Guide](docs/configuration-guide.md)** - Options pattern and validation
- **[Performance Guide](docs/performance-guide.md)** - GC optimization and performance tuning
- **[Logging Guide](docs/logging-guide.md)** - Structured logging with source-generated log messages
- **[Testing Guide](docs/testing-guide.md)** - Testing with TUnit, assertions, and best practices
- **[Wolverine Integration](docs/wolverine-guide.md)** - Command/handler pattern with Wolverine
- **[API Conventions](docs/api-conventions-guide.md)** - Time handling and JSON serialization standards
- **[API Client Generation](docs/api-client-generation.md)** - Automated client generation with OpenAPI and Refitter
- **[ETag Support](docs/etag-guide.md)** - Optimistic concurrency and caching
- **[Correlation & Causation IDs](docs/correlation-causation-guide.md)** - Distributed tracing
- **[Caching Guide](docs/caching-guide.md)** - Hybrid caching with Redis and localization support
- **[Real-time Notifications](docs/signalr-guide.md)** - SignalR integration and optimistic updates
- **[Contributing Guidelines](CONTRIBUTING.md)** - How to contribute to this project

## 🔧 Technology Stack

### Frontend
- **Blazor Web** - Interactive web UI with Server rendering
- **SignalR Client** - Real-time notifications
- **BookStore.Client** - Reusable API client library (Refit-based)
- **Polly** - Resilience and transient fault handling

### Backend
- **ASP.NET Core 10** - Minimal APIs
- **C# 14** - Latest language features (collection expressions, primary constructors, etc.)
- **Marten 8.17** - Event store and document DB
- **Wolverine 5.9** - Mediator, message bus, and SignalR integration
- **PostgreSQL 16** - Database with pg_trgm and unaccent extensions

### Infrastructure
- **.NET Aspire** - Orchestration and observability
- **OpenTelemetry** - Distributed tracing and metrics
- **Scalar** - API documentation UI
- **Docker** - Container runtime
- **TUnit** - Modern testing framework with built-in code coverage
- **Roslyn Analyzers** - Custom analyzers for Event Sourcing/CQRS patterns
- **Roslynator.Analyzers 4.15.0** - Enhanced code analysis
- **Refit** - Type-safe REST library for .NET
- **Refitter** - Tool to generate Refit interfaces from OpenAPI specifications
- **NSwag** - OpenAPI client generation (optional development tool)

## 📊 API Endpoints

### Public Endpoints

- `GET /api/books/search` - Search books with pagination
- `GET /api/books/{id}` - Get book by ID (with ETag)
- `GET /api/authors` - List authors
- `GET /api/categories` - List categories (localized)
- `GET /api/publishers` - List publishers

### Admin Endpoints

- `POST /api/admin/books` - Create book
- `PUT /api/admin/books/{id}` - Update book (with If-Match)
- `DELETE /api/admin/books/{id}` - Soft delete book
- `POST /api/admin/books/{id}/restore` - Restore book
- Similar CRUD for authors, categories, publishers
- `POST /api/admin/projections/rebuild` - Rebuild projections

## 🌍 Localization Example

```bash
# Get categories in Portuguese
curl -H "Accept-Language: pt-PT" http://localhost:5000/api/categories

# Create category with translations
curl -X POST http://localhost:5000/api/admin/categories \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Software Architecture",
    "translations": {
      "en": {"name": "Software Architecture"},
      "pt": {"name": "Arquitetura de Software"}
    }
  }'
```

## 🔄 Event Sourcing Example

```bash
# All operations create events in the event store

# Create a book
curl -X POST http://localhost:5000/api/admin/books \
  -H "X-Correlation-ID: workflow-123" \
  -d '{"title": "Clean Code", ...}'
# → BookAdded event stored

# Update the book
curl -X PUT http://localhost:5000/api/admin/books/{id} \
  -H "X-Correlation-ID: workflow-123" \
  -H "If-Match: \"1\"" \
  -d '{"title": "Clean Code (Updated)", ...}'
# → BookUpdated event stored

# View all events for this workflow
SELECT * FROM mt_events 
WHERE correlation_id = 'workflow-123';
```

## 🛡️ Optimistic Concurrency with ETags

```bash
# Get book (receives ETag)
curl -i http://localhost:5000/api/books/{id}
# ETag: "5"

# Update with concurrency check
curl -X PUT http://localhost:5000/api/admin/books/{id} \
  -H "If-Match: \"5\"" \
  -d '{"title": "Updated Title", ...}'
# Success → ETag: "6"

# Concurrent update fails
curl -X PUT http://localhost:5000/api/admin/books/{id} \
  -H "If-Match: \"5\"" \
  -d '{"title": "Another Update", ...}'
# Error: 412 Precondition Failed
```

## 🔍 Monitoring

- **Health Checks**: `/health`
- **Aspire Dashboard**: `https://localhost:17161`
- **Scalar API Docs**: `/scalar/v1`
- **OpenAPI Spec**: `/openapi/v1.json`

## 🧪 Testing

The project uses **TUnit**, a modern testing framework with built-in code coverage and parallel execution.

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test --project src/BookStore.Tests/BookStore.Tests.csproj

# Alternative: Run tests directly
dotnet run --project src/BookStore.Tests/BookStore.Tests.csproj
```

> [!NOTE]
> TUnit uses Microsoft.Testing.Platform on .NET 10+. The `global.json` file configures the test runner automatically.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Copyright (c) 2025 Antao Almada

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guidelines](CONTRIBUTING.md) for details on:

- How to report issues
- How to suggest features
- Development setup and workflow
- Coding standards and best practices
- Pull request process

By contributing, you agree that your contributions will be licensed under the MIT License.


