using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Tenant-related log messages for multi-tenancy operations.
/// </summary>
public static partial class Log
{
    public static partial class Tenants
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Tenant {TenantId} accessing {Method} {Path} from {RemoteIp}")]
        public static partial void TenantAccess(
            ILogger logger,
            string tenantId,
            string method,
            string path,
            string? remoteIp);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Cross-tenant access attempted. User Tenant: {UserTenant}, Target Tenant: {TargetTenant}")]
        public static partial void CrossTenantAccessAttempted(
            ILogger logger,
            string userTenant,
            string targetTenant);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid tenant requested: {TenantId}")]
        public static partial void InvalidTenantRequested(
            ILogger logger,
            string tenantId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeding admin user for tenant {TenantId} (Session Tenant: {SessionTenant})")]
        public static partial void SeedingAdminUser(
            ILogger logger,
            string tenantId,
            string sessionTenant);
    }
}
