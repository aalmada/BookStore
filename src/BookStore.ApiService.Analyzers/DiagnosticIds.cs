using Microsoft.CodeAnalysis;

namespace BookStore.ApiService.Analyzers;

/// <summary>
/// Diagnostic IDs for BookStore.ApiService architectural rules
/// </summary>
public static class DiagnosticIds
{
    // Event Rules (BS1xxx)
    public const string EventMustBeRecord = "BS1001";
    public const string EventMustBeImmutable = "BS1002";
    public const string EventMustBeInEventsNamespace = "BS1003";

    // Command Rules (BS2xxx)
    public const string CommandMustBeRecord = "BS2001";
    public const string CommandMustBeInCommandsNamespace = "BS2002";
    public const string CommandPropertiesShouldUseInit = "BS2003";

    // Aggregate Rules (BS3xxx)
    public const string ApplyMethodMustReturnVoid = "BS3001";
    public const string ApplyMethodMustHaveOneParameter = "BS3002";
    public const string ApplyMethodShouldBePrivate = "BS3003";
    public const string AggregateCommandMethodShouldReturnEvent = "BS3004";
    public const string AggregatePropertyShouldNotHavePublicSetter = "BS3005";

    // Handler Rules (BS4xxx)
    public const string HandlerMethodShouldBeNamedHandle = "BS4001";
    public const string HandlerMethodShouldBeStatic = "BS4002";
    public const string HandlerFirstParameterShouldBeCommand = "BS4003";

    // Namespace Organization Rules (BS5xxx)
    public const string EventTypeMustBeInEventsNamespace = "BS5001";
    public const string CommandTypeMustBeInCommandsNamespace = "BS5002";
    public const string AggregateTypeMustBeInAggregatesNamespace = "BS5003";
    public const string HandlerTypeMustBeInHandlersNamespace = "BS5004";
    public const string ProjectionTypeMustBeInProjectionsNamespace = "BS5005";
}

/// <summary>
/// Category constants for diagnostics
/// </summary>
public static class DiagnosticCategories
{
    public const string EventSourcing = "EventSourcing";
    public const string CQRS = "CQRS";
    public const string DomainModel = "DomainModel";
    public const string Architecture = "Architecture";
}
