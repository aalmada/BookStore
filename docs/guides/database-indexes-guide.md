# Database Indexes Guide

> **Note**: This guide focuses on production database optimization. For multi-tenancy architecture and implementation details, see [Multi-Tenancy Guide](multi-tenancy-guide.md).

## Overview

This document provides recommended database indexes for optimal multi-tenancy performance in production environments. These indexes are critical for ensuring fast query performance as tenant data grows.

## Marten Multi-Tenancy Indexes

Marten automatically creates a `tenant_id` column on all multi-tenanted documents. However, additional indexes can significantly improve query performance.

### Core Tenant Indexes

```sql
-- Index on tenant_id for all multi-tenanted document tables
-- Marten creates these automatically, but verify they exist:

CREATE INDEX IF NOT EXISTS idx_book_tenant_id 
    ON mt_doc_book(tenant_id);

CREATE INDEX IF NOT EXISTS idx_author_tenant_id 
    ON mt_doc_author(tenant_id);

CREATE INDEX IF NOT EXISTS idx_publisher_tenant_id 
    ON mt_doc_publisher(tenant_id);

CREATE INDEX IF NOT EXISTS idx_category_tenant_id 
    ON mt_doc_category(tenant_id);

CREATE INDEX IF NOT EXISTS idx_user_tenant_id 
    ON mt_doc_user(tenant_id);
```

### Composite Indexes for Common Queries

```sql
-- Book queries often filter by tenant + deletion status
CREATE INDEX idx_book_tenant_deleted 
    ON mt_doc_book(tenant_id, (data->>'IsDeleted'));

-- Search queries filter by tenant + title
CREATE INDEX idx_book_tenant_title 
    ON mt_doc_book(tenant_id, (data->>'Title'));

-- Author lookups by tenant + name
CREATE INDEX idx_author_tenant_name 
    ON mt_doc_author(tenant_id, (data->>'Name'));

-- User lookups by tenant + email
CREATE INDEX idx_user_tenant_email 
    ON mt_doc_user(tenant_id, (data->>'Email'));
```

### Event Store Indexes

```sql
-- Tenant-specific event queries
CREATE INDEX idx_events_tenant_stream 
    ON mt_events(tenant_id, stream_id);

CREATE INDEX idx_events_tenant_timestamp 
    ON mt_events(tenant_id, timestamp DESC);
```

### Full-Text Search Indexes

```sql
-- Full-text search on book titles and descriptions
CREATE INDEX idx_book_fulltext 
    ON mt_doc_book 
    USING gin(to_tsvector('english', data->>'Title' || ' ' || data->'Translations'->'en'->>'Description'));

-- Tenant-specific full-text search
CREATE INDEX idx_book_tenant_fulltext 
    ON mt_doc_book(tenant_id) 
    INCLUDE (data) 
    WHERE (data->>'IsDeleted')::boolean = false;
```

## Performance Monitoring

### Query Performance Analysis

```sql
-- Find slow queries by tenant
SELECT 
    tenant_id,
    COUNT(*) as query_count,
    AVG(total_time) as avg_time_ms,
    MAX(total_time) as max_time_ms
FROM pg_stat_statements
WHERE query LIKE '%mt_doc_%'
GROUP BY tenant_id
ORDER BY avg_time_ms DESC;

-- Check index usage
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan as index_scans,
    idx_tup_read as tuples_read,
    idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
WHERE tablename LIKE 'mt_doc_%'
ORDER BY idx_scan DESC;

-- Find missing indexes
SELECT 
    schemaname,
    tablename,
    seq_scan,
    seq_tup_read,
    idx_scan,
    seq_tup_read / seq_scan as avg_seq_read
FROM pg_stat_user_tables
WHERE schemaname = 'public'
  AND tablename LIKE 'mt_doc_%'
  AND seq_scan > 0
ORDER BY seq_tup_read DESC;
```

## Maintenance

### Index Rebuild Schedule

```sql
-- Rebuild indexes monthly to prevent bloat
REINDEX INDEX CONCURRENTLY idx_book_tenant_id;
REINDEX INDEX CONCURRENTLY idx_author_tenant_id;
-- ... repeat for all indexes
```

### Vacuum Strategy

```sql
-- Vacuum tenant tables weekly
VACUUM ANALYZE mt_doc_book;
VACUUM ANALYZE mt_doc_author;
VACUUM ANALYZE mt_doc_user;
```

## Marten Configuration

### Enable Index Logging

```csharp
// In MartenConfigurationExtensions.cs
options.Logger(new ConsoleMartenLogger());

// Log all SQL queries in development
if (builder.Environment.IsDevelopment())
{
    options.Logger(new ConsoleMartenLogger());
}
```

### Custom Index Configuration

```csharp
// Add custom indexes via Marten
options.Schema.For<Book>()
    .Index(x => x.TenantId, x => x.Title);

options.Schema.For<Author>()
    .Index(x => x.TenantId, x => x.Name);
```

## Production Checklist

- [ ] Verify all tenant_id indexes exist
- [ ] Add composite indexes for common query patterns
- [ ] Enable query logging to identify slow queries
- [ ] Set up index monitoring alerts
- [ ] Schedule monthly index rebuilds
- [ ] Configure autovacuum for tenant tables
- [ ] Test query performance with production-like data volumes

## Expected Performance

With proper indexing:
- Tenant-filtered queries: < 10ms for up to 1M records per tenant
- Full-text search: < 50ms for up to 100K books per tenant
- Event queries: < 5ms for recent events

## Troubleshooting

### Slow Queries

1. Check if index is being used: `EXPLAIN ANALYZE <query>`
2. Verify index exists: `\di` in psql
3. Check index bloat: See maintenance section
4. Consider adding composite index for specific query pattern

### High Memory Usage

1. Reduce index size with partial indexes
2. Use `INCLUDE` clause instead of full column indexes
3. Schedule index rebuilds during off-peak hours
