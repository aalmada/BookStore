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
# Prerequisites: .NET 10 SDK, .NET Aspire workload, Docker Desktop

# Install Aspire workload (if not already installed)
dotnet workload install aspire

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
- **Responsive Design** for desktop and mobile
- **Type-safe API Client** with Refit
- **Resilience** with Polly (retry and circuit breaker)

### Backend API
- **Event Sourcing** with Marten and PostgreSQL
- **CQRS** with async projections for optimized reads
- **Multi-language Support** for categories (en, pt, es, fr, de)
- **Full-text Search** with PostgreSQL trigrams and unaccent
- **Optimistic Concurrency** with ETags
- **Distributed Tracing** with correlation/causation IDs
- **API Versioning** (header-based, v1.0)
- **Soft Deletion** with restore capability
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
│   ├── BookStore.ApiService/      # Backend API with event sourcing
│   │   ├── Aggregates/            # Domain aggregates
│   │   ├── Events/                # Domain events
│   │   ├── Commands/              # Command definitions
│   │   ├── Handlers/              # Wolverine command handlers
│   │   ├── Projections/           # Read model projections
│   │   ├── Endpoints/             # API endpoints
│   │   └── Infrastructure/        # Cross-cutting concerns
│   │
│   ├── BookStore.Web/             # Blazor frontend
│   │   ├── Components/            # Blazor components
│   │   ├── Services/              # API client (Refit)
│   │   └── Models/                # DTOs and view models
│   │
│   ├── BookStore.AppHost/         # Aspire orchestration
│   │   └── Program.cs             # Service configuration
│   │
│   ├── BookStore.ServiceDefaults/ # Shared configuration
│   │   └── Extensions.cs          # OpenTelemetry, health checks
│   │
│   └── BookStore.Tests/           # Unit tests
│       ├── Handlers/              # Handler tests
│       └── JsonSerializationTests.cs
│
├── docs/                          # Documentation
│   ├── getting-started.md         # Setup guide
│   ├── architecture.md            # System design
│   ├── wolverine-guide.md         # Command/handler pattern
│   ├── time-standards.md          # JSON and time standards
│   ├── etag-guide.md              # ETag usage
│   └── correlation-causation-guide.md
│
├── BookStore.slnx                 # Solution file (new .slnx format)
└── README.md                      # This file
```

## 📖 Documentation

- **[Getting Started](docs/getting-started.md)** - Setup and first steps
- **[Architecture Overview](docs/architecture.md)** - System design and patterns
- **[Wolverine Integration](docs/wolverine-guide.md)** - Command/handler pattern with Wolverine
- **[Time Standards](docs/time-standards.md)** - JSON serialization and UTC standards
- **[ETag Support](docs/etag-guide.md)** - Optimistic concurrency and caching
- **[Correlation & Causation IDs](docs/correlation-causation-guide.md)** - Distributed tracing
- **[Contributing Guidelines](CONTRIBUTING.md)** - How to contribute to this project

## 🔧 Technology Stack

### Frontend
- **Blazor Web** - Interactive web UI
- **Refit** - Type-safe HTTP client
- **Polly** - Resilience and transient fault handling

### Backend
- **ASP.NET Core 10** - Minimal APIs
- **C# 14** - Latest language features (collection expressions, primary constructors, etc.)
- **Marten 8.17** - Event store and document DB
- **Wolverine 5.9** - Mediator and message bus
- **PostgreSQL 16** - Database with pg_trgm and unaccent extensions

### Infrastructure
- **.NET Aspire** - Orchestration and observability
- **OpenTelemetry** - Distributed tracing and metrics
- **Scalar** - API documentation UI
- **Docker** - Container runtime
- **Roslynator.Analyzers 4.15.0** - Enhanced code analysis

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
curl -H "Accept-Language: pt-BR" http://localhost:5000/api/categories

# Create category with translations
curl -X POST http://localhost:5000/api/admin/categories \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Software Architecture",
    "translations": {
      "pt": {"name": "Arquitetura de Software"},
      "es": {"name": "Arquitectura de Software"}
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

```bash
# Run tests
dotnet test

# View test coverage
dotnet test /p:CollectCoverage=true
```

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

## 📚 Learn More

- [Marten - Event Store & Document DB](https://martendb.io/)
- [Wolverine - Mediator & Message Bus](https://wolverine.netlify.app/)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [PostgreSQL Trigram (pg_trgm)](https://www.postgresql.org/docs/current/pgtrgm.html)
