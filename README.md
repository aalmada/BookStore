# Book Store

[![CI](https://github.com/aalmada/BookStore/actions/workflows/ci.yml/badge.svg)](https://github.com/aalmada/BookStore/actions/workflows/ci.yml)
![Code Coverage](https://img.shields.io/badge/Code%20Coverage-from%20CI-green?logo=codecov)
[![Nightly Integration Tests](https://github.com/aalmada/BookStore/actions/workflows/nightly-integration.yml/badge.svg)](https://github.com/aalmada/BookStore/actions/workflows/nightly-integration.yml)
[![Documentation](https://github.com/aalmada/BookStore/actions/workflows/docs.yml/badge.svg)](https://github.com/aalmada/BookStore/actions/workflows/docs.yml)
[![CodeQL](https://github.com/aalmada/BookStore/actions/workflows/codeql.yml/badge.svg)](https://github.com/aalmada/BookStore/actions/workflows/codeql.yml)
[![License](https://img.shields.io/github/license/aalmada/BookStore)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

Full-stack .NET online book store application with event-sourced backend API and Blazor frontend, orchestrated by Aspire.

## Overview

This project is a demonstration and exploration of modern .NET technologies, designed to be as complete as possible while strictly following architectural best practices, and keeping performance and scalability as core priorities.

I am sure a lot may be improved. Opening this code to the public is an opportunity to get feedback and learn from others' contributions.

A complete book store management system featuring:
- **Backend API**: Event-sourced ASP.NET Core Minimal APIs with Marten and PostgreSQL
- **Frontend**: Blazor web application for browsing and managing books
- **Orchestration**: Aspire for local development, deployment, and observability
- **Database**: PostgreSQL with event store and read model projections
- **Modern Stack**: .NET 10 with C# 14 (latest language features)

> [!TIP]
> **🤖 AI-Ready Development**: This project is built for AI agents. Run `/doctor` to check your environment, then use skills like `/scaffold-write` or `/scaffold-read` to generate code following all architectural patterns automatically. **Zero setup required**—just ask your AI assistant to scaffold a feature!
>
> - ✅ **AGENTS.md Files** - Context-aware guidance in every project directory
> - ✅ **10+ Skills** - Automated workflows for scaffolding, testing, and deployment
> - ✅ **Template-Based** - Generate Event Sourcing, CQRS, and reactive UI code
> - ✅ **Standards Compliant** - Follows [GitHub Copilot](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills) and [agents.md](https://agents.md/) specifications
>
> See [Agent Development Guide](docs/agent-guide.md) for the complete system overview.

## 🏗️ Architectural Philosophy

> *"A complex system that works is invariably found to have evolved from a simple system that worked. A complex system designed from scratch never works and cannot be patched up to make it work. You have to start over with a working simple system."*
>
> — **John Gall**

This project deliberately moves away from the "microservices-first" dogma, instead embracing a **Modular Monolith** approach.

There is a growing industry consensus that starting with microservices introduces accidental complexity—distributed transactions, network latency, and infrastructure overhead—before domain boundaries are strictly defined. This solution provides a concrete example of how to build a scalable, robust system without the premature complexity of a distributed mesh.

The architecture emphasizes:

- **Modularity**: Loose coupling is enforced using **Event Sourcing** and **CQRS**. Features communicate via messages (Wolverine), ensuring that future decomposition into services is a seamless refactoring rather than a rewrite.

- **Pragmatism & Performance**: We prioritize clean, maintainable code over academic purity. By avoiding excessive abstraction layers (like generic repositories and passthrough services), we eliminate "architectural tax," ensuring the code remains easy to refactor and runs with maximum performance.

- **Completeness**: Unlike typical "Hello World" demos, this project implements production-grade requirements: resiliency, distributed tracing, structured logging, correct HTTP semantics, optimistic concurrency, hybrid caching, configuration validation, content localization, scalable real-time updates, passwordless authentication, comprehensive testing, and trigram‑based search for fast, flexible text matching.

- **Simplicity**: By keeping the deployment unit single but the code modular, we gain the benefits of microservices (isolation, maintainability) without the operational drawbacks.

This serves as a foundational blueprint that scales *with* your needs, allowing you to evolve from a simple, working system into a complex one naturally.

## 🚀 Quick Start

```bash
# Prerequisites: .NET 10 SDK, Aspire CLI, Docker Desktop

# Install Aspire CLI: Follow instructions at https://aspire.dev/get-started/install-cli/

# Clone and run
# HTTPS
git clone https://github.com/aalmada/BookStore.git
# OR SSH
git clone git@github.com:aalmada/BookStore.git

cd BookStore
dotnet restore
aspire run
```

The Aspire dashboard opens automatically, providing access to:
- **Web Frontend** - Blazor application for browsing books
- **API Service** - Backend API with Scalar documentation at `/api-reference`
- **PostgreSQL** - Event store and read model database
- **PgAdmin** - Database management interface

## ✨ Features

### Frontend (Blazor Web)
- **Book Catalog** with search and filtering
- **Book Details** with comprehensive information
- **Real-time Updates** with Server-Sent Events (SSE) for push notifications
- **Optimistic UI** for instant feedback with eventual consistency
- **Responsive Design** for desktop and mobile
- **Type-safe API Client** with BookStore.Client library (Refit-based)
- **Resilience** with Polly (retry and circuit breaker)

### Backend API
- **Event Sourcing** with Marten and PostgreSQL
- **CQRS** with async projections for optimized reads
- **Real-time Notifications** with Server-Sent Events (SSE) - Automatic push notifications for all mutations
- **JWT Authentication** - Secure token-based authentication for all clients (Web & Mobile)
- **Passwordless Support** - Full Passkey support including **Passkey-First Sign Up** (.NET 10)
- **Role-Based Authorization** - Admin endpoints protected
- **Multi-language Support** (configurable via `appsettings.json`)
- **Full-text Search** with PostgreSQL trigrams and unaccent
- **Optimistic Concurrency** with ETags
- **Hybrid Caching** - Multi-tiered caching with Redis and in-memory support
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

### Infrastructure (Aspire)
- **Service Orchestration** for local development
- **Service Discovery** between frontend and backend
- **OpenTelemetry** integration for observability
- **Container Management** for PostgreSQL and PgAdmin
- **Dashboard** for monitoring all services

## 📁 Project Structure

For a detailed breakdown of the project structure, please refer to the [Getting Started Guide](docs/getting-started.md#project-structure).

## 📖 Documentation

- **[Getting Started](docs/getting-started.md)** - Setup and first steps
- **[Architecture Overview](docs/architecture.md)** - System design and patterns
- **[Event Sourcing Guide](docs/event-sourcing-guide.md)** - Event sourcing concepts and implementation
- **[Aspire Orchestration Guide](docs/aspire-guide.md)** - Service orchestration and local development
- **[Marten Guide](docs/marten-guide.md)** - Document DB and Event Store features
- **[Wolverine Integration](docs/wolverine-guide.md)** - Command/handler pattern with Wolverine
- **[Configuration Guide](docs/configuration-guide.md)** - Options pattern and validation
- **[API Conventions](docs/api-conventions-guide.md)** - Time handling and JSON serialization standards
- **[API Client Generation](docs/api-client-generation.md)** - Type-safe API client with Refit
- **[Authentication Guide](docs/authentication-guide.md)** - JWT authentication and role-based authorization
- **[Passkey Guide](docs/passkey-guide.md)** - Passwordless authentication with WebAuthn/FIDO2
- **[Real-time Notifications](#) <!-- TODO: Create SSE guide -->** - Server-Sent Events (SSE) for push notifications
- **[Logging Guide](docs/logging-guide.md)** - Structured logging with source-generated log messages
- **[Correlation & Causation IDs](docs/correlation-causation-guide.md)** - Distributed tracing
- **[Localization Guide](docs/localization-guide.md)** - Multi-language support
- **[Caching Guide](docs/caching-guide.md)** - Hybrid caching with Redis and localization support
- **[ETag Support](docs/etag-guide.md)** - Optimistic concurrency and caching
- **[Performance Guide](docs/performance-guide.md)** - GC optimization and performance tuning
- **[Testing Guide](docs/testing-guide.md)** - Unit testing with TUnit, assertions, and best practices
- **[Integration Testing Guide](docs/integration-testing-guide.md)** - End-to-end testing with Aspire and Bogus
- **[Agent Development Guide](docs/agent-guide.md)** - AI assistant configuration system (AGENTS.md files and Claude skills)
- **[Aspire Deployment Guide](docs/aspire-deployment-guide.md)** - Deploy to Azure and Kubernetes
- **[Production Scaling Guide](docs/production-scaling-guide.md)** - Scale applications and databases in production
- **[Contributing Guidelines](CONTRIBUTING.md)** - How to contribute to this project

## 🔧 Technology Stack

### Frontend
- **Blazor Web** - Interactive web UI with Server rendering
- **Server-Sent Events (SSE)** - Real-time push notifications from server
- **BookStore.Client** - Reusable API client library (Refit-based)
- **Polly** - Resilience and transient fault handling

### Backend
- **ASP.NET Core 10** - Minimal APIs
- **C# 14** - Latest language features (collection expressions, primary constructors, etc.)
- **Marten** - Event store and document DB
- **Wolverine** - Mediator, message bus, and async projections
- **PostgreSQL 16** - Database with pg_trgm and unaccent extensions

### Infrastructure
- **Aspire** - Orchestration and observability
- **OpenTelemetry** - Distributed tracing and metrics
- **Scalar** - API documentation UI
- **Polly** - Resilience and transient fault handling
- **Docker** - Container runtime
- **TUnit** - Modern testing framework with built-in code coverage
- **Bogus** - Fake data generation for tests
- **Roslyn Analyzers** - Custom analyzers for Event Sourcing/CQRS patterns
- **Roslynator.Analyzers** - Enhanced code analysis
- **Refit** - Type-safe REST library for .NET

## 📊 API Endpoints

### Public Endpoints

- `GET /api/books` - List and search books (search with `?search=query`)
- `GET /api/books/{id}` - Get book by ID (with ETag)
- `GET /api/authors` - List authors
- `GET /api/categories` - List categories (localized)
- `GET /api/publishers` - List publishers

### Identity Endpoints

**Authentication:**
- `POST /identity/register` - Register new user
- `POST /identity/login` - Login and receive JWT access token
- `POST /identity/refresh` - Refresh JWT access token
- `POST /identity/logout` - Logout (invalidate token/session)

**Passkey (Passwordless):**
- `POST /account/attestation/options` - Get passkey creation options
- `POST /account/attestation/result` - Complete passkey registration / Sign up
- `POST /account/assertion/options` - Get passkey login options
- `POST /account/assertion/result` - Login with passkey

**Account Management:**
- `POST /identity/forgotPassword` - Request password reset
- `POST /identity/resetPassword` - Reset password
- `GET /identity/manage/info` - Get user information
- `POST /identity/manage/info` - Update user information

### Admin Endpoints

> [!NOTE]
> Admin endpoints require authentication with the `Admin` role. Include the JWT token in the `Authorization: Bearer <token>` header.

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
- **Scalar API Docs**: `/api-reference`
- **OpenAPI Spec**: `/openapi/v1.json`

## 🧪 Testing

The project uses **TUnit**, a modern testing framework with built-in code coverage and parallel execution.

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test --project tests/ApiService/BookStore.ApiService.UnitTests/BookStore.ApiService.UnitTests.csproj

# Alternative: Run tests directly
dotnet run --project tests/ApiService/BookStore.ApiService.UnitTests/BookStore.ApiService.UnitTests.csproj
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

## 🤖 Agent-Based Development

This project is fully configured for **AI-assisted development**, providing comprehensive guidance for AI coding assistants to understand and work effectively with the codebase.

### AGENTS.md Files

Throughout the project, you'll find `AGENTS.md` files that serve as knowledge bases for AI assistants:

- **[Root AGENTS.md](AGENTS.md)** - Project overview, architectural principles, and general guidelines
- **[ApiService AGENTS.md](src/BookStore.ApiService/AGENTS.md)** - Backend patterns, Event Sourcing/CQRS conventions, and API design
- **[Web AGENTS.md](src/BookStore.Web/AGENTS.md)** - Blazor frontend patterns, real-time updates, and UI conventions
- **[Client AGENTS.md](src/Client/BookStore.Client/AGENTS.md)** - API client library usage and patterns
- **[Test Projects AGENTS.md](tests/BookStore.ApiService.Tests/AGENTS.md)** - Testing patterns and integration test setup

These files enable AI assistants to quickly understand context-specific patterns, conventions, and architectural decisions without needing to explore the entire codebase.

### Reusable Skills

The [`.claude/skills/`](.claude/skills/) directory contains reusable AI workflows for common development tasks:

- **[scaffold-skill](.claude/skills/scaffold-skill/SKILL.md)** - Create new AI skills with proper structure
- **[create-new-endpoint](.claude/skills/create-new-endpoint/SKILL.md)** - Generate CQRS endpoints following project conventions
- **[create-domain-type](.claude/skills/create-domain-type/SKILL.md)** - Scaffold domain entities with Event Sourcing patterns

These skills encode complex workflows and best practices, enabling consistent, high-quality code generation across the project.

For a comprehensive guide on the agent development system, see the **[Agent Development Guide](docs/agent-guide.md)**.


