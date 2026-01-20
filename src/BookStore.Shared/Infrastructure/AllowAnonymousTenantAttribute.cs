namespace BookStore.Shared.Infrastructure;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AllowAnonymousTenantAttribute : Attribute
{
}
