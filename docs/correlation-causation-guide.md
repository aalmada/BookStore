# Causation and Correlation ID Guide

## Overview

The Book Store API implements **causation** and **correlation** IDs for distributed tracing and event chain tracking. This enables you to trace the entire lifecycle of a business transaction across multiple services and events.

## Concepts

### Correlation ID
- **Purpose**: Tracks an entire business transaction from start to finish
- **Scope**: Remains the same throughout the entire workflow
- **Use Case**: Trace all events related to a single user action (e.g., "Create a book with authors and categories")

### Causation ID
- **Purpose**: Tracks the immediate cause of an event
- **Scope**: Changes with each event in the chain
- **Use Case**: Understand what triggered a specific event (e.g., "This projection update was caused by a BookAdded event")

### Event ID
- **Purpose**: Unique identifier for each specific event
- **Scope**: Unique per event
- **Use Case**: Reference a specific event in the event store

## HTTP Headers

### Request Headers

| Header | Description | Required | Example |
|--------|-------------|----------|---------|
| `X-Correlation-ID` | Business transaction identifier | No* | `550e8400-e29b-41d4-a716-446655440000` |
| `X-Causation-ID` | Immediate cause identifier | No* | `660e8400-e29b-41d4-a716-446655440001` |

*If not provided, the system will auto-generate these IDs

### Response Headers

| Header | Description | Example |
|--------|-------------|---------|
| `X-Correlation-ID` | Echo of correlation ID (or generated) | `550e8400-e29b-41d4-a716-446655440000` |
| `X-Event-ID` | ID of the event created by this request | `770e8400-e29b-41d4-a716-446655440002` |

## Event Metadata Structure

Every event in the system includes metadata:

```json
{
  "eventId": "770e8400-e29b-41d4-a716-446655440002",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "causationId": "660e8400-e29b-41d4-a716-446655440001",
  "userId": "admin@example.com",
  "timestamp": "2024-12-24T12:00:00Z"
}
```

## Usage Examples

### Example 1: Simple Book Creation

**Request**:
```bash
curl -X POST http://localhost:5000/api/admin/books \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: txn-12345" \
  -d '{
    "title": "Clean Code",
    "isbn": "978-0132350884",
    "description": "A handbook of agile software craftsmanship",
    "publisherId": "pub-001",
    "authorIds": ["author-001"],
    "categoryIds": ["cat-001"]
  }'
```

**Response Headers**:
```
X-Correlation-ID: txn-12345
X-Event-ID: evt-67890
```

**Event Stored**:
```json
{
  "eventType": "BookAdded",
  "data": {
    "id": "book-001",
    "title": "Clean Code",
    "metadata": {
      "eventId": "evt-67890",
      "correlationId": "txn-12345",
      "causationId": "txn-12345",  // Same as correlation (root event)
      "timestamp": "2024-12-24T12:00:00Z"
    }
  }
}
```

### Example 2: Event Chain - Update Following Creation

**Step 1: Create Book**
```bash
curl -X POST http://localhost:5000/api/admin/books \
  -H "X-Correlation-ID: workflow-abc123" \
  -H "Content-Type: application/json" \
  -d '{"title": "Domain-Driven Design", ...}'
```

Response: `X-Event-ID: evt-create-001`

**Step 2: Update Book (using previous event as causation)**
```bash
curl -X PUT http://localhost:5000/api/admin/books/book-001 \
  -H "X-Correlation-ID: workflow-abc123" \
  -H "X-Causation-ID: evt-create-001" \
  -H "Content-Type: application/json" \
  -d '{"title": "Domain-Driven Design (Revised)", ...}'
```

Response: `X-Event-ID: evt-update-001`

**Event Chain**:
```
workflow-abc123 (Correlation ID)
  ├─ evt-create-001 (BookAdded)
  │   └─ causationId: workflow-abc123
  └─ evt-update-001 (BookUpdated)
      └─ causationId: evt-create-001
```

### Example 3: Distributed Workflow

Imagine a workflow where creating a book triggers multiple operations:

**1. Create Publisher**
```bash
POST /api/admin/publishers
X-Correlation-ID: import-2024-001
```
Response: `X-Event-ID: evt-pub-001`

**2. Create Author**
```bash
POST /api/admin/authors
X-Correlation-ID: import-2024-001
X-Causation-ID: evt-pub-001
```
Response: `X-Event-ID: evt-auth-001`

**3. Create Category**
```bash
POST /api/admin/categories
X-Correlation-ID: import-2024-001
X-Causation-ID: evt-auth-001
```
Response: `X-Event-ID: evt-cat-001`

**4. Create Book**
```bash
POST /api/admin/books
X-Correlation-ID: import-2024-001
X-Causation-ID: evt-cat-001
{
  "publisherId": "...",
  "authorIds": ["..."],
  "categoryIds": ["..."]
}
```
Response: `X-Event-ID: evt-book-001`

**Complete Event Chain**:
```
import-2024-001 (Correlation ID - Book Import Workflow)
  ├─ evt-pub-001 (PublisherAdded)
  │   └─ causationId: import-2024-001
  ├─ evt-auth-001 (AuthorAdded)
  │   └─ causationId: evt-pub-001
  ├─ evt-cat-001 (CategoryAdded)
  │   └─ causationId: evt-auth-001
  └─ evt-book-001 (BookAdded)
      └─ causationId: evt-cat-001
```

## Querying Events by Correlation ID

You can query the event store to find all events related to a correlation ID:

```sql
-- PostgreSQL query in Marten event store
SELECT 
    id,
    type,
    data->>'eventId' as event_id,
    data->'metadata'->>'correlationId' as correlation_id,
    data->'metadata'->>'causationId' as causation_id,
    timestamp
FROM mt_events
WHERE data->'metadata'->>'correlationId' = 'import-2024-001'
ORDER BY timestamp;
```

## Best Practices

### 1. Always Propagate Correlation ID
When making multiple related API calls, always use the same correlation ID:
```bash
CORRELATION_ID="workflow-$(date +%s)"

# All related calls use the same correlation ID
curl -H "X-Correlation-ID: $CORRELATION_ID" ...
curl -H "X-Correlation-ID: $CORRELATION_ID" ...
```

### 2. Use Event IDs as Causation IDs
When one operation triggers another, use the previous event ID as the causation ID:
```bash
# First call
RESPONSE=$(curl -i -X POST ... -H "X-Correlation-ID: $CORRELATION_ID")
EVENT_ID=$(echo "$RESPONSE" | grep "X-Event-ID" | cut -d' ' -f2)

# Second call caused by first
curl -H "X-Correlation-ID: $CORRELATION_ID" \
     -H "X-Causation-ID: $EVENT_ID" \
     ...
```

### 3. Generate Meaningful Correlation IDs
Use descriptive correlation IDs for easier debugging:
```bash
# Good
X-Correlation-ID: book-import-2024-12-24-batch-001

# Also good
X-Correlation-ID: user-registration-john-doe-20241224

# Less helpful
X-Correlation-ID: 12345
```

### 4. Log Correlation IDs
Always log correlation IDs in your application logs:
```csharp
logger.LogInformation(
    "Processing book creation. CorrelationId: {CorrelationId}, CausationId: {CausationId}",
    metadata.CorrelationId,
    metadata.CausationId);
```

## Debugging with Correlation IDs

### Scenario: Find all events in a failed workflow

1. **Get the correlation ID** from your application logs or error message
2. **Query the event store**:
   ```sql
   SELECT * FROM mt_events 
   WHERE data->'metadata'->>'correlationId' = 'failed-workflow-123'
   ORDER BY timestamp;
   ```
3. **Analyze the event chain** to find where the workflow failed

### Scenario: Trace projection updates

1. **Find the source event** that triggered a projection update
2. **Use the causation ID** to link back to the original command
3. **Follow the correlation ID** to see the entire business transaction

## Integration with Observability Tools

Correlation and causation IDs integrate seamlessly with:

- **Application Insights**: Automatically tracked in telemetry
- **Seq**: Structured logging with correlation ID filtering
- **Jaeger/Zipkin**: Distributed tracing with span correlation
- **OpenTelemetry**: Native support for trace context propagation

Example with OpenTelemetry:
```csharp
Activity.Current?.SetTag("correlation.id", metadata.CorrelationId);
Activity.Current?.SetTag("causation.id", metadata.CausationId);
```

## Summary

- **Correlation ID**: Tracks the entire business workflow
- **Causation ID**: Tracks immediate event causes
- **Event ID**: Unique identifier for each event
- **Headers**: `X-Correlation-ID`, `X-Causation-ID`, `X-Event-ID`
- **Automatic**: System generates IDs if not provided
- **Propagation**: Always returned in response headers
