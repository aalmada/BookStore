# Passkey Security Fix

## Overview

This document describes critical security fixes applied to the passkey authentication system to prevent duplicate user accounts and authentication failures.

## Vulnerabilities Fixed

### 1. **Duplicate User Registration**
- **Issue**: Multiple users could be created with the same email address within a tenant
- **Impact**: Passkey authentication would fail with "user handle mismatch" errors
- **Root Cause**: Unique index on `NormalizedEmail` was global instead of tenant-scoped, and missing explicit email checks before user creation

### 2. **Race Condition in User Creation**
- **Issue**: Concurrent registration attempts could bypass uniqueness checks
- **Impact**: Same as above
- **Root Cause**: Email uniqueness only checked during `CreateAsync`, not before

### 3. **Anti-Enumeration Logic Creating Wrong User IDs**
- **Issue**: When a user already existed, new passkey options were generated with a different user ID
- **Impact**: Passkey would be bound to wrong user ID, causing login failures
- **Root Cause**: Conflicting user detection returned new ID instead of existing user's ID

## Changes Made

### Code Changes

#### 1. [PasskeyEndpoints.cs](../../src/BookStore.ApiService/Endpoints/PasskeyEndpoints.cs)

**Registration Flow (`/attestation/options`)**:
- Now returns existing user's ID when email conflict detected
- Passkey is bound to correct user from the start

**Registration Flow (`/attestation/result`)**:
- Added explicit email conflict check before user creation
- Prevents race conditions and database constraint violations

**Login Flow (`/assertion/options`)**:
- Added defensive logging for debugging user/passkey mismatches

**Login Flow (`/assertion/result`)**:
- Added credential ownership validation
- Verifies the credential actually belongs to the authenticated user

#### 2. [MartenConfigurationExtensions.cs](../../src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs)

**Database Schema**:
```csharp
// Before (BROKEN - global uniqueness)
.UniqueIndex(UniqueIndexType.Computed, x => x.NormalizedEmail!)

// After (FIXED - per-tenant uniqueness)
.UniqueIndex(UniqueIndexType.DuplicatedField, x => x.NormalizedEmail!)
```

Changed from `Computed` to `DuplicatedField` to create tenant-scoped unique indexes.

## Required Actions

### For Development

1. **Drop and recreate the database** to apply the new index structure:
   ```bash
   # Stop Aspire
   aspire stop

   # Clear PostgreSQL data
   docker volume rm bookstore_postgres-data
   # OR manually drop the database

   # Restart Aspire
   aspire run
   ```

2. **Clear browser passkeys** associated with localhost:
   - Chrome: `chrome://settings/passkeys`
   - Edge: `edge://settings/passkeys`
   - Delete all passkeys for `localhost`

3. **Re-register test accounts** with fresh passkeys

### For Production

⚠️ **BEFORE** deploying to production:

1. **Audit existing users** for duplicates:
   ```sql
   SELECT tenant_id,
          data->>'normalizedEmail' as email,
          COUNT(*) as count
   FROM public.mt_doc_applicationuser
   GROUP BY tenant_id, data->>'normalizedEmail'
   HAVING COUNT(*) > 1;
   ```

2. **Resolve duplicates** if found:
   - Identify which user should be kept (usually the oldest)
   - Migrate passkeys from duplicate accounts to the primary account
   - Delete duplicate accounts
   - See [Duplicate User Resolution Script](#duplicate-user-resolution)

3. **Apply database migration**:
   - Marten will automatically recreate indexes on next startup
   - Or manually drop and recreate indexes (see below)

4. **Notify affected users** to re-register their passkeys if needed

## Duplicate User Resolution

If duplicate users exist in production:

```sql
-- 1. Find duplicates
WITH duplicates AS (
  SELECT
    tenant_id,
    data->>'normalizedEmail' as email,
    jsonb_agg(
      jsonb_build_object(
        'id', id,
        'created_at', data->>'createdAt',
        'passkey_count', jsonb_array_length(COALESCE(data->'passkeys', '[]'::jsonb))
      ) ORDER BY (data->>'createdAt')::timestamp
    ) as users
  FROM public.mt_doc_applicationuser
  GROUP BY tenant_id, data->>'normalizedEmail'
  HAVING COUNT(*) > 1
)
SELECT * FROM duplicates;

-- 2. For each duplicate group, keep the oldest user with passkeys
-- and delete others (or migrate their passkeys first)
-- Manual intervention required - this is tenant/case specific
```

## Manual Index Recreation (Production)

If you need to recreate indexes without dropping the database:

```sql
-- Drop old indexes
DROP INDEX IF EXISTS mt_doc_applicationuser_uidx_normalized_email;
DROP INDEX IF EXISTS mt_doc_applicationuser_uidx_normalized_user_name;

-- Create new tenant-scoped unique indexes
CREATE UNIQUE INDEX mt_doc_applicationuser_uidx_normalized_email
  ON public.mt_doc_applicationuser (tenant_id, (data->>'normalizedEmail'));

CREATE UNIQUE INDEX mt_doc_applicationuser_uidx_normalized_user_name
  ON public.mt_doc_applicationuser (tenant_id, (data->>'normalizedUserName'));
```

## Testing

After applying the fix, verify:

1. ✅ **Can register new user with passkey**
2. ✅ **Can login with passkey**
3. ✅ **Cannot register duplicate email** (should silently succeed but not create duplicate)
4. ✅ **No user handle mismatch errors**
5. ✅ **Multi-tenant isolation works** (same email can exist in different tenants)

## Rollback Plan

If issues occur after deployment:

1. Monitor for errors in logs: `"Passkey assertion failed"`, `"User handle mismatch"`
2. If widespread issues, rollback code to previous version
3. Database indexes can remain (they're compatible with old code)
4. Investigate specific failing scenarios before re-deploying fix

## Related Files

- [PasskeyEndpoints.cs](../../src/BookStore.ApiService/Endpoints/PasskeyEndpoints.cs)
- [MartenConfigurationExtensions.cs](../../src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs)
- [Passkey Guide](./passkey-guide.md) (User-facing documentation)

## References

- [WebAuthn Specification](https://www.w3.org/TR/webauthn/)
- [ASP.NET Identity Passkey Support](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-passkeys)
- [Marten Multi-Tenancy](https://martendb.io/documents/multi-tenancy.html)
- [Marten Indexing](https://martendb.io/documents/indexing/)
