using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Models;

/// <summary>
/// Configuration options for pagination
/// </summary>
public sealed class PaginationOptions : IValidatableObject
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Pagination";

    /// <summary>
    /// Default value for page size when not specified
    /// </summary>
    public const int DefaultPageSizeValue = 20;

    /// <summary>
    /// Default value for maximum number of items allowed per page
    /// </summary>
    public const int MaxPageSizeValue = 100;

    /// <summary>
    /// Default page size when not specified
    /// </summary>
    [Range(1, 1000, ErrorMessage = "DefaultPageSize must be between 1 and 1000")]
    public int DefaultPageSize { get; init; } = DefaultPageSizeValue;

    /// <summary>
    /// Maximum number of items allowed per page
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxPageSize must be between 1 and 1000")]
    public int MaxPageSize { get; init; } = MaxPageSizeValue;

    /// <summary>
    /// Validates that DefaultPageSize is less than or equal to MaxPageSize
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DefaultPageSize > MaxPageSize)
        {
            yield return new ValidationResult(
                $"DefaultPageSize ({DefaultPageSize}) cannot be greater than MaxPageSize ({MaxPageSize})",
                [nameof(DefaultPageSize), nameof(MaxPageSize)]);
        }
    }
}
