# Getting Started with Book Store

This guide will help you set up and run the complete Book Store application (frontend + backend) on your local machine using .NET Aspire.

## Prerequisites

### Required Software

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
  - Includes **C# 14** with latest language features (collection expressions, primary constructors, etc.)
- **.NET Aspire workload** - Install after .NET SDK:
  ```bash
  dotnet workload install aspire
  ```
- **Docker Desktop** - [Download](https://www.docker.com/products/docker-desktop)
- **Git** - [Download](https://git-scm.com/downloads)

### Recommended Tools

- **Visual Studio 2024** or **VS Code** with C# extension
- **Postman** or **curl** for API testing
- **pgAdmin** (included in Docker setup) for database inspection

## Installation

### 1. Clone the Repository

```bash
git clone <repository-url>
cd BookStore
```

### 2. Verify Docker is Running

```bash
docker --version
docker ps
```

Make sure Docker Desktop is running and healthy.

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Run the Application with Aspire

```bash
aspire run
```

This will:
- Start the .NET Aspire orchestrator
- Launch PostgreSQL and PgAdmin containers via Docker
- Start the backend API service
- Start the Blazor web frontend
- Open the Aspire dashboard in your browser

## Accessing the Application

### Aspire Dashboard

The Aspire dashboard opens automatically or visit:
```
https://localhost:17161/login?t=<token>
```

From the dashboard you can:
- **View all services**: Frontend, API, PostgreSQL, PgAdmin
- **Check resource health**: CPU, memory, status
- **Access logs**: Real-time logs for each service
- **Find service URLs**: Click on any service to get its endpoint

### Web Frontend (Blazor)

1. In the Aspire dashboard, click on **web** (BookStore.Web)
2. Click the HTTP endpoint URL (e.g., `http://localhost:5001`)
3. Browse the book catalog, search for books, and view details

The frontend provides:
- **Book Catalog**: Browse and search all available books
- **Book Details**: View comprehensive information about each book
- **Responsive Design**: Works on desktop and mobile devices

### API Documentation (Scalar)

1. In the Aspire dashboard, click on **apiservice**
2. Copy the HTTP endpoint URL (e.g., `http://localhost:5000`)
3. Navigate to `http://localhost:5000/scalar/v1`

You'll see interactive API documentation where you can:
- Browse all endpoints (public and admin)
- Test API calls directly from the browser
- View request/response schemas
- See code examples in multiple languages

### Database (PgAdmin)

1. In the Aspire dashboard, click on **pgadmin**
2. Login with credentials (shown in Aspire dashboard)
3. Connect to the PostgreSQL server
4. Explore the event store and projections:
   - `mt_events` - All domain events (event sourcing)
   - `mt_streams` - Event streams per aggregate
   - `book_search_projection` - Book search read model
   - `author_projection`, `category_projection`, `publisher_projection`

## First API Calls

### 1. Create a Publisher

```bash
curl -X POST http://localhost:5000/api/admin/publishers \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: getting-started-001" \
  -d '{
    "name": "O'\''Reilly Media"
  }'
```

Response:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "correlationId": "getting-started-001"
}
```

### 2. Create an Author

```bash
curl -X POST http://localhost:5000/api/admin/authors \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: getting-started-001" \
  -d '{
    "name": "Martin Fowler",
    "biography": "Software development expert and author"
  }'
```

### 3. Create a Category

```bash
curl -X POST http://localhost:5000/api/admin/categories \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: getting-started-001" \
  -d '{
    "name": "Software Architecture",
    "description": "Books about software design and architecture",
    "translations": {
      "pt": {
        "name": "Arquitetura de Software",
        "description": "Livros sobre design e arquitetura de software"
      }
    }
  }'
```

### 4. Create a Book

```bash
curl -X POST http://localhost:5000/api/admin/books \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: getting-started-001" \
  -d '{
    "title": "Patterns of Enterprise Application Architecture",
    "isbn": "978-0321127426",
    "description": "Classic book on enterprise application patterns",
    "publicationDate": "2002-11-15",
    "publisherId": "550e8400-e29b-41d4-a716-446655440000",
    "authorIds": ["<author-id-from-step-2>"],
    "categoryIds": ["<category-id-from-step-3>"]
  }'
```

### 5. Search for Books

```bash
curl "http://localhost:5000/api/books/search?q=patterns"
```

### 6. Get Categories in Portuguese

```bash
curl -H "Accept-Language: pt-BR" http://localhost:5000/api/categories
```

## Exploring the Event Store

### View Events in PostgreSQL

```sql
-- Connect to the database via PgAdmin

-- View all events
SELECT 
    id,
    seq_id,
    type,
    timestamp,
    correlation_id,
    causation_id
FROM mt_events
ORDER BY timestamp DESC
LIMIT 10;

-- View events for a specific correlation ID
SELECT * FROM mt_events 
WHERE correlation_id = 'getting-started-001'
ORDER BY timestamp;

-- View a specific stream
SELECT * FROM mt_streams 
WHERE id = '<book-id>';
```

## Project Structure

```
BookStore/
├── src/
│   ├── BookStore.ApiService/      # Backend API
│   │   ├── Aggregates/            # Domain aggregates (event sourcing)
│   │   ├── Events/                # Domain events
│   │   ├── Commands/              # Command definitions
│   │   ├── Handlers/              # Wolverine command handlers
│   │   ├── Projections/           # Read models (CQRS)
│   │   ├── Endpoints/             # API endpoints
│   │   │   ├── Admin/             # Admin CRUD endpoints
│   │   │   ├── BookEndpoints.cs   # Public book endpoints
│   │   │   └── ...                # Other public endpoints
│   │   ├── Infrastructure/        # Cross-cutting concerns
│   │   └── Program.cs             # API entry point
│   │
│   ├── BookStore.Web/             # Blazor Frontend
│   │   ├── Components/            # Blazor components
│   │   │   ├── Pages/             # Page components
│   │   │   └── Layout/            # Layout components
│   │   ├── Services/              # API client (Refit)
│   │   ├── Models/                # DTOs and view models
│   │   └── Program.cs             # Frontend entry point
│   │
│   ├── BookStore.AppHost/         # Aspire Orchestration
│   │   └── Program.cs             # Service configuration
│   │
│   ├── BookStore.ServiceDefaults/ # Shared Configuration
│   │   └── Extensions.cs          # OpenTelemetry, logging, health checks
│   │
│   └── BookStore.Tests/           # Unit Tests
│       ├── Handlers/              # Handler tests
│       └── JsonSerializationTests.cs
│
└── docs/                          # Documentation
    ├── getting-started.md         # This guide
    ├── architecture.md            # System design
    ├── wolverine-guide.md         # Command/handler pattern
    ├── time-standards.md          # JSON and time standards
    └── ...                        # Other guides
```

## Testing

The project uses **TUnit**, a modern testing framework for .NET with built-in code coverage and source-generated tests.

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test --project src/BookStore.Tests/BookStore.Tests.csproj

# Alternative: Run tests directly
dotnet run --project src/BookStore.Tests/BookStore.Tests.csproj
```

### Test Structure

- **Handler Tests** - Test Wolverine command handlers with mocked dependencies
- **JSON Serialization Tests** - Verify API serialization standards (ISO 8601, camelCase, etc.)
- **Integration Tests** - Test the full application stack with Aspire.Hosting.Testing

All tests use TUnit's fluent assertion syntax:
```csharp
await Assert.That(result).IsNotNull();
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(collection).Contains(item);
```

> [!NOTE]
> TUnit provides built-in code coverage without additional packages. Tests run in parallel by default for improved performance.

## Development Workflow

### 1. Make Code Changes

Edit files in `src/ApiService/BookStore.ApiService/`

### 2. Hot Reload

The application supports hot reload. Changes to code will automatically rebuild and restart.

### 3. Test Changes

Use Scalar UI or curl to test your changes:

```bash
# Test endpoint
curl http://localhost:5000/api/books/search?q=test

# Check logs in Aspire dashboard
```

### 4. View Events

Check PgAdmin to see events being stored:

```sql
SELECT * FROM mt_events ORDER BY timestamp DESC LIMIT 5;
```

## Common Tasks

### Rebuild Projections

```bash
curl -X POST http://localhost:5000/api/admin/projections/rebuild
```

### Check Projection Status

```bash
curl http://localhost:5000/api/admin/projections/status
```

### Health Check

```bash
curl http://localhost:5000/health
```

## Troubleshooting

### Docker Not Running

**Error**: "Container runtime 'docker' was found but appears to be unhealthy"

**Solution**: 
1. Open Docker Desktop
2. Wait for it to fully start
3. Run `aspire run` again

### Port Already in Use

**Error**: "Address already in use"

**Solution**:
```bash
# Find process using port
lsof -i :5000

# Kill the process
kill -9 <PID>
```

### Database Connection Issues

**Error**: "Could not connect to PostgreSQL"

**Solution**:
1. Check Docker is running
2. Check Aspire dashboard for PostgreSQL status
3. Verify connection string in `appsettings.json`

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Next Steps

- **[Architecture Overview](architecture.md)** - Understand the system design
- **[Testing Guide](testing-guide.md)** - Learn about testing with TUnit
- **[Event Sourcing Guide](event-sourcing-guide.md)** - Learn about event sourcing
- **[ETag Support](etag-guide.md)** - Implement optimistic concurrency

## Getting Help

- Check the [API Reference](api-reference.md) for endpoint details
- Review [Architecture](architecture.md) for design patterns
- Explore the Scalar UI for interactive documentation
- Check Aspire dashboard logs for debugging
