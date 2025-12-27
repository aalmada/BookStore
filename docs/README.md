# Book Store API Documentation

Welcome to the Book Store API documentation. This is an event-sourced book store management system built with ASP.NET Core, Marten, and PostgreSQL.

## 📚 Table of Contents

- [Getting Started](getting-started.md)
- [Architecture Overview](architecture.md)
- [API Reference](api-reference.md)
- [Event Sourcing Guide](event-sourcing.md)
- [Wolverine Integration](wolverine-guide.md)
- [Correlation & Causation IDs](correlation-causation-guide.md)
- [ETag Support](etag-guide.md)
- [Localization](localization.md)
- [Deployment](deployment.md)

## 🚀 Quick Start

```bash
# Prerequisites
- .NET 10 SDK
- Docker Desktop (for PostgreSQL)

# Clone and run
git clone <repository-url>
cd BookStore
dotnet restore
aspire run
```

Visit `http://localhost:17161` for the Aspire dashboard and navigate to the API service to access Scalar documentation at `/scalar/v1`.

## 🏗️ Project Structure

```
BookStore/
├── src/
│   ├── ApiService/
│   │   ├── BookStore.ApiService/           # Main API with event sourcing
│   │   ├── BookStore.ApiService.Analyzers/ # Roslyn analyzers for code quality
│   │   ├── BookStore.ApiService.Analyzers.Tests/ # Analyzer tests
│   │   └── BookStore.ApiService.Tests/     # API unit tests
│   ├── Web/
│   │   ├── BookStore.Web/                  # Blazor frontend
│   │   └── BookStore.Web.Tests/            # Web integration tests
│   ├── BookStore.AppHost/                  # Aspire orchestration
│   └── BookStore.ServiceDefaults/          # Shared configuration
├── docs/                                   # Documentation
└── README.md
```

## ✨ Key Features

- **Event Sourcing** with Marten and PostgreSQL
- **CQRS** with async projections
- **Multi-language Support** for categories
- **Full-text Search** with PostgreSQL trigrams
- **Optimistic Concurrency** with ETags
- **Distributed Tracing** with correlation/causation IDs
- **API Versioning** (header-based)
- **Soft Deletion** across all entities
- **OpenAPI** documentation with Scalar UI

## 📖 Documentation Guides

### For Developers

- **[Getting Started](getting-started.md)** - Setup and first steps
- **[Architecture Overview](architecture.md)** - System design and patterns
- **[Event Sourcing Guide](event-sourcing.md)** - Understanding the event store
- **[API Reference](api-reference.md)** - Complete endpoint documentation

### For API Consumers

- **[API Reference](api-reference.md)** - All available endpoints
- **[ETag Support](etag-guide.md)** - Optimistic concurrency and caching
- **[Correlation & Causation IDs](correlation-causation-guide.md)** - Distributed tracing
- **[Localization](localization.md)** - Multi-language support

### For Operations

- **[Deployment](deployment.md)** - Production deployment guide
- **[Monitoring](monitoring.md)** - Observability and health checks

## 🔗 Quick Links

- **API Documentation**: `/scalar/v1` (when running)
- **Aspire Dashboard**: `http://localhost:17161`
- **Health Checks**: `/health`
- **OpenAPI Spec**: `/openapi/v1.json`

## 📝 License

[Your License Here]

## 🤝 Contributing

[Contributing Guidelines]
