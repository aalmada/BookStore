# Production Scaling Guide

This guide documents scaling controls that are currently implemented in BookStore.

## Current Scaling Model

BookStore scales horizontally at the application tier:

- `BookStore.ApiService` instances are stateless and can be replicated
- `BookStore.Web` instances are stateless (Blazor Server + backend APIs) and can be replicated
- PostgreSQL is the single primary data store for Marten event and document storage
- Redis is used for distributed cache and notification fan-out patterns

This repository does not currently include committed Kubernetes manifests, HPA resources, or a production `azure.yaml` deployment definition.

## Implemented Controls

### 1. Tenant-Aware Rate Limiting

Rate limiting is implemented in `src/BookStore.ApiService/Infrastructure/Extensions/RateLimitingExtensions.cs`.

- Global limiter partitions by tenant (`tenantId`) and enforces fixed windows
- Auth policy partitions by `tenantId:ip`
- SSE policy uses token-bucket limits per `tenantId:ip`

Configured defaults in `src/BookStore.ApiService/appsettings.json`:

- `PermitLimit`: 1000 / minute (global)
- `AuthPermitLimit`: 20 / 60s
- `NotificationSseTokenLimit`: 20, `TokensPerPeriod`: 2 / second

Development overrides in `src/BookStore.ApiService/appsettings.Development.json` increase auth/SSE budgets for local testing.

### 2. Cache-Backed Read Scaling

HybridCache is used for read-heavy endpoints.

- L1 in-memory + L2 distributed cache
- Tag-based invalidation after successful mutations
- Tenant-aware keying and localized variants

See `docs/guides/caching-guide.md` for key/tag conventions.

### 3. Async Projection Throughput

Marten projections are configured async and integrated with Wolverine.

- `DaemonMode.Solo` is currently configured
- Projection commit listeners invalidate cache tags and emit notification events

This keeps writes responsive while read models catch up asynchronously.

### 4. Real-Time Fan-Out

SSE is used for live UI refreshes.

- API stream endpoint: `/api/notifications/stream`
- Query invalidation in web app maps notifications to query keys
- Redis-backed notification service supports multi-instance propagation

See `docs/guides/real-time-notifications.md`.

## Operational Guidance

### Capacity Tuning Order

1. Tune rate limits for workload and tenant mix
2. Validate cache hit rates and tag invalidation behavior
3. Profile projection lag under peak write load
4. Scale API/Web replicas behind your platform load balancer
5. Monitor DB CPU, locks, and I/O before adding architectural complexity

### Database Considerations

- Current architecture assumes a single PostgreSQL primary
- Indexes are configured in Marten schema code
- For large tenant growth, evolve toward tenant segmentation/sharding plans

See `docs/guides/database-indexes-guide.md`.

## Not Yet Implemented In Repo

The following may be valid future options but are not currently implemented in repository deployment assets:

- Kubernetes HPA manifests
- Azure Container Apps autoscale definitions committed as IaC
- Dedicated read replicas in app data access paths
- External connection poolers (for example PgBouncer) in runtime topology

## Verification Checklist

Before increasing production traffic:

- `RateLimit` values reviewed for expected tenant traffic
- Cache keys/tags reviewed for hot endpoints
- SSE limits tested under burst connections
- Projection lag tested under write bursts
- Health endpoints (`/health`, `/alive`) integrated into platform probes
