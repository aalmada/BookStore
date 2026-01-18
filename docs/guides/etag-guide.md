# ETag Support in Book Store API

## Overview

The Book Store API implements **ETags (Entity Tags)** for:
1. **Optimistic Concurrency Control** - Prevent conflicting updates
2. **HTTP Caching** - Reduce bandwidth and improve performance

ETags are generated from Marten's event stream versions, ensuring they accurately reflect the current state of resources.

## How ETags Work

### ETag Generation
```
ETag = "stream_version"
Example: "5" (indicates this is version 5 of the resource)
```

Every time a resource is modified (updated, deleted, restored), the stream version increments, and the ETag changes.

## Read Operations (GET)

### Get Book by ID

**Request**:
```bash
GET /api/books/{id}
```

**Response** (First Request):
```http
HTTP/1.1 200 OK
ETag: "3"
Content-Type: application/json

{
  "id": "book-123",
  "title": "Clean Code",
  ...
}
```

**Conditional Request** (Subsequent):
```bash
GET /api/books/{id}
If-None-Match: "3"
```

**Response** (Not Modified):
```http
HTTP/1.1 304 Not Modified
ETag: "3"
```

**Response** (Modified):
```http
HTTP/1.1 200 OK
ETag: "4"
Content-Type: application/json

{
  "id": "book-123",
  "title": "Clean Code (Updated)",
  ...
}
```

### Benefits
- ✅ **Bandwidth savings**: No body sent with 304 responses
- ✅ **Reduced server load**: Cached responses when content unchanged
- ✅ **Automatic**: Browsers and HTTP clients handle this automatically

## Write Operations (PUT/DELETE)

### Update Book

**Step 1: Get Current Version**
```bash
GET /api/books/{id}
```

Response includes `ETag: "3"`

**Step 2: Update with If-Match**
```bash
PUT /api/admin/books/{id}
If-Match: "3"
Content-Type: application/json

{
  "title": "Clean Code (Updated)",
  ...
}
```

**Success Response**:
```http
HTTP/1.1 204 No Content
ETag: "4"
```

**Conflict Response** (Someone else updated it):
```http
HTTP/1.1 412 Precondition Failed
Content-Type: application/problem+json

{
  "title": "Precondition Failed",
  "detail": "The resource has been modified since you last retrieved it. Please refresh and try again.",
  "status": 412
}
```

### Soft Delete Book

```bash
DELETE /api/admin/books/{id}
If-Match: "4"
```

**Success**:
```http
HTTP/1.1 204 No Content
ETag: "5"
```

### Restore Book

```bash
POST /api/admin/books/{id}/restore
If-Match: "5"
```

**Success**:
```http
HTTP/1.1 204 No Content
ETag: "6"
```

## Client Implementation Examples

### JavaScript/TypeScript

```typescript
class BookApiClient {
  private baseUrl = 'http://localhost:5000/api';
  
  async getBook(id: string): Promise<{ book: Book; etag: string }> {
    const response = await fetch(`${this.baseUrl}/books/${id}`);
    const etag = response.headers.get('ETag') || '';
    const book = await response.json();
    return { book, etag };
  }
  
  async updateBook(id: string, book: UpdateBookRequest, etag: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/admin/books/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': etag
      },
      body: JSON.stringify(book)
    });
    
    if (response.status === 412) {
      throw new Error('Book was modified by another user. Please refresh and try again.');
    }
    
    if (!response.ok) {
      throw new Error(`Update failed: ${response.statusText}`);
    }
  }
  
  async getBookWithCache(id: string, cachedETag?: string): Promise<Book | null> {
    const headers: HeadersInit = {};
    if (cachedETag) {
      headers['If-None-Match'] = cachedETag;
    }
    
    const response = await fetch(`${this.baseUrl}/books/${id}`, { headers });
    
    if (response.status === 304) {
      return null; // Use cached version
    }
    
    return await response.json();
  }
}
```

### C# HttpClient

```csharp
public class BookApiClient
{
    private readonly HttpClient _httpClient;
    
    public async Task<(Book Book, string ETag)> GetBookAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"/api/books/{id}");
        response.EnsureSuccessStatusCode();
        
        var etag = response.Headers.ETag?.Tag ?? "";
        var book = await response.Content.ReadFromJsonAsync<Book>();
        
        return (book!, etag);
    }
    
    public async Task UpdateBookAsync(Guid id, UpdateBookRequest request, string etag)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/books/{id}")
        {
            Content = JsonContent.Create(request)
        };
        requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue(etag));
        
        var response = await _httpClient.SendAsync(requestMessage);
        
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new InvalidOperationException(
                "Book was modified by another user. Please refresh and try again.");
        }
        
        response.EnsureSuccessStatusCode();
    }
}
```

### Python Requests

```python
import requests

class BookApiClient:
    def __init__(self, base_url='http://localhost:5000/api'):
        self.base_url = base_url
    
    def get_book(self, book_id):
        response = requests.get(f'{self.base_url}/books/{book_id}')
        response.raise_for_status()
        return {
            'book': response.json(),
            'etag': response.headers.get('ETag', '')
        }
    
    def update_book(self, book_id, book_data, etag):
        response = requests.put(
            f'{self.base_url}/admin/books/{book_id}',
            json=book_data,
            headers={'If-Match': etag}
        )
        
        if response.status_code == 412:
            raise ValueError('Book was modified by another user. Please refresh.')
        
        response.raise_for_status()
        return response.headers.get('ETag', '')
```

## Workflow Examples

### Example 1: Safe Update Workflow

```bash
# 1. Get current book
GET /api/books/123
# Response: ETag: "5"

# 2. User edits book in UI

# 3. Submit update with ETag
PUT /api/admin/books/123
If-Match: "5"
{
  "title": "Updated Title",
  ...
}

# Success: ETag: "6"
```

### Example 2: Concurrent Update Detection

```bash
# User A gets book
GET /api/books/123
# Response: ETag: "5"

# User B gets book
GET /api/books/123
# Response: ETag: "5"

# User B updates first
PUT /api/admin/books/123
If-Match: "5"
# Success: ETag: "6"

# User A tries to update
PUT /api/admin/books/123
If-Match: "5"
# Error: 412 Precondition Failed (ETag mismatch)

# User A refreshes and gets new version
GET /api/books/123
# Response: ETag: "6"

# User A updates with new ETag
PUT /api/admin/books/123
If-Match: "6"
# Success: ETag: "7"
```

### Example 3: Efficient Caching

```bash
# First request
GET /api/books/123
# Response: 200 OK, ETag: "5", Full body

# Subsequent request (within cache period)
GET /api/books/123
If-None-Match: "5"
# Response: 304 Not Modified (no body, saves bandwidth)

# After someone updates the book
GET /api/books/123
If-None-Match: "5"
# Response: 200 OK, ETag: "6", Full body (content changed)
```

## Error Handling

### 412 Precondition Failed

**Cause**: The `If-Match` ETag doesn't match the current resource version

**Client Action**:
1. Notify user that the resource was modified
2. Fetch the latest version
3. Ask user to review changes and resubmit

**Example**:
```typescript
try {
  await updateBook(id, data, etag);
} catch (error) {
  if (error.status === 412) {
    // Fetch latest version
    const { book, etag: newETag } = await getBook(id);
    
    // Show user the conflict
    showConflictDialog({
      yourChanges: data,
      currentVersion: book,
      onResolve: (resolved) => updateBook(id, resolved, newETag)
    });
  }
}
```

## Best Practices

### For Clients

1. **Always store ETags** when fetching resources
2. **Always send If-Match** for PUT/DELETE operations
3. **Handle 412 gracefully** - don't just retry
4. **Use If-None-Match** for GET requests to leverage caching
5. **Don't ignore ETags** - they prevent data loss

### For UI Applications

```typescript
// Good: Store ETag with resource
interface BookState {
  book: Book;
  etag: string;
  lastFetched: Date;
}

// Good: Validate before update
async function saveBook(state: BookState, changes: Partial<Book>) {
  try {
    await api.updateBook(state.book.id, changes, state.etag);
  } catch (error) {
    if (error.status === 412) {
      // Refresh and ask user to review
      const latest = await api.getBook(state.book.id);
      showConflictResolution(state.book, latest.book, changes);
    }
  }
}
```

### For Batch Operations

```typescript
// Process updates sequentially to handle conflicts
async function batchUpdate(books: Array<{id: string, data: any, etag: string}>) {
  const results = [];
  
  for (const book of books) {
    try {
      await api.updateBook(book.id, book.data, book.etag);
      results.push({ id: book.id, success: true });
    } catch (error) {
      if (error.status === 412) {
        results.push({ id: book.id, success: false, reason: 'conflict' });
      } else {
        results.push({ id: book.id, success: false, reason: 'error' });
      }
    }
  }
  
  return results;
}
```

## Testing ETags

### Manual Testing with curl

```bash
# Get book and extract ETag
curl -i http://localhost:5000/api/books/123
# Note the ETag header

# Update with correct ETag (should succeed)
curl -X PUT http://localhost:5000/api/admin/books/123 \
  -H "If-Match: \"5\"" \
  -H "Content-Type: application/json" \
  -d '{"title": "Updated"}'

# Update with wrong ETag (should fail with 412)
curl -X PUT http://localhost:5000/api/admin/books/123 \
  -H "If-Match: \"999\"" \
  -H "Content-Type: application/json" \
  -d '{"title": "Updated"}'

# Test caching with If-None-Match
curl -i http://localhost:5000/api/books/123 \
  -H "If-None-Match: \"5\""
# Should return 304 if not modified
```

## Summary

- **Read Operations**: Use `If-None-Match` for efficient caching (304 responses)
- **Write Operations**: Use `If-Match` for optimistic concurrency (prevent conflicts)
- **ETags**: Generated from Marten stream versions (auto-incremented)
- **Error Handling**: 412 Precondition Failed when version mismatch
- **Client Responsibility**: Store ETags, handle conflicts gracefully
