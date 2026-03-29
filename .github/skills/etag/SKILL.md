---
name: etag
description: Use this skill for any request involving HTTP ETags, conditional requests, or optimistic concurrency in REST APIs: implementing/explaining ETag headers, preventing lost updates, designing cache validation or conditional GET/PUT/DELETE, explaining If-Match, If-None-Match, 304 Not Modified, or 412 Precondition Failed, or any .NET/web/REST API project where resource versioning or concurrency is needed; always use when ETag, If-Match, If-None-Match, conditional requests, or optimistic concurrency are mentioned.
---

# ETag Skill

This skill provides guidance and best practices for using HTTP ETags in REST APIs for cache validation and optimistic concurrency control.

## What is an ETag?
- The HTTP `ETag` (entity tag) response header is an identifier for a specific version of a resource.
- ETags enable efficient caching and help prevent simultaneous updates from overwriting each other ("mid-air collisions").
- If a resource changes, a new ETag value must be generated.

## Syntax
```
ETag: "<etag_value>"
ETag: W/"<etag_value>"   # Weak validator
```
- Strong ETags: Byte-for-byte identical content
- Weak ETags: Semantically equivalent, but not byte-for-byte identical

## Typical Patterns
- **Cache validation**: Client sends `If-None-Match` with cached ETag; server replies 304 Not Modified if unchanged.
- **Optimistic concurrency**: Client sends `If-Match` with ETag; server only updates if ETag matches current version, else 412 Precondition Failed.

## Example: Avoiding Lost Updates
1. Client GETs resource, receives ETag: `ETag: "abc123"`
2. Client modifies resource, sends PUT with `If-Match: "abc123"`
3. Server compares ETag; if matches, updates resource and returns new ETag. If not, returns 412 Precondition Failed.

## Example: Caching
1. Client GETs resource, receives ETag: `ETag: "abc123"`
2. On next GET, client sends `If-None-Match: "abc123"`
3. If unchanged, server returns 304 Not Modified (no body)

## Related Headers
- `If-Match`: Only perform operation if ETag matches
- `If-None-Match`: Only perform operation if ETag does not match
- `If-Modified-Since`, `If-Unmodified-Since`: Date-based alternatives
- `304 Not Modified`: Resource unchanged
- `412 Precondition Failed`: ETag did not match

## Best Practices
- Use strong ETags for byte-accurate validation; weak ETags for semantic equivalence
- Always generate a new ETag when resource changes
- Never reuse ETags for different content
- ETag generation methods include:
  - Hash of resource content (collision-resistant preferred)
  - Hash of last modification timestamp
  - Revision number or version ID
- ETags should be unique per resource version and content-coding aware (e.g., gzip vs. plain)
- Avoid weak hash/checksum functions (e.g., weaker than CRC32)
- For APIs, use ETags for update/delete concurrency, not just caching
- In .NET, use middleware or manual header management to implement ETag logic

- Always quote ETag values (e.g., "abc123"); use the optional W/ prefix for weak validators (e.g., W/"abc123").
- Each representation (e.g., gzip, br, different formats) should have its own ETag.
- For strictly versioned resources, use a version number as a strong ETag.
- For non-versioned or compressed resources, use a hash of the content as a strong or weak ETag as appropriate.
- If last-modified time is sufficient, consider using the Last-Modified header instead of ETag for simplicity.

## Common Misuses
- Unquoted ETag values (e.g., ETag: abc123)
- English words or placeholders (e.g., ETag: default, ETag: $etagFile)
- Template values not replaced (e.g., ETag: "MyCalculatedEtagValue")
- ETags with spaces (e.g., ETag: "3/19/2017 6:35:34 PM")
- Double-weak ETags (e.g., W/W/"abc123")
- Not updating ETag after resource changes, causing stale content

## Practical Example
```http
GET /resource HTTP/1.1
Host: example.com

HTTP/1.1 200 OK
ETag: "ebeb4dbc1362d124452335a71286c21d"
Cache-Control: max-age=0, must-revalidate

# Client revalidates:
GET /resource HTTP/1.1
Host: example.com
If-None-Match: "ebeb4dbc1362d124452335a71286c21d"

HTTP/1.1 304 Not Modified
```
## Caveats
- Incorrect ETag handling (e.g., not updating after resource change) can cause clients to receive stale data
- ETags can be abused for user tracking ("cookieless cookies"); be mindful of privacy implications


## References
- [MDN: ETag](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/ETag)
- [RFC 7232: HTTP/1.1 Conditional Requests](https://www.rfc-editor.org/rfc/rfc7232)
- [Wikipedia: HTTP ETag](https://en.wikipedia.org/wiki/HTTP_ETag)
- [Fastly: ETags - What they are, and how to use them](https://www.fastly.com/blog/etags-what-they-are-and-how-to-use-them)
- [If-Match](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/If-Match), [If-None-Match](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/If-None-Match)
- [304 Not Modified](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/304), [412 Precondition Failed](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/412)

---

For more on ETag generation, strong/weak validation, and privacy, see the [Wikipedia article](https://en.wikipedia.org/wiki/HTTP_ETag) and [RFC 7232 Section 2.3](https://tools.ietf.org/html/rfc7232#section-2.3).

---

For implementation details in .NET, Node.js, or other stacks, see the language/framework-specific guides or ask for code examples.
