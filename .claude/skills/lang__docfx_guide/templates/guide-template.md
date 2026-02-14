# {Title}

{Brief introduction explaining what this guide covers, why it matters, and when to use it. Keep this to 1-2 paragraphs.}

## Prerequisites

{List what readers need to know or have set up before following this guide. Examples:}

- Basic understanding of {concept}
- {Tool or SDK} installed (version X.Y or higher)
- Completed the [Getting Started Guide](../getting-started.md)

{If no prerequisites, remove this section.}

## Core Concepts

{Explain fundamental concepts that readers need to understand. Use subsections if needed.}

### {Concept 1}

{Explanation with examples}

### {Concept 2}

{Explanation with examples}

## Getting Started

{Quick start section showing the simplest use case or most common scenario.}

### Step 1: {Action}

{Instructions}

```csharp
// Example code
public record {ExampleCommand}(Guid Id, string Name);
```

### Step 2: {Action}

{Instructions}

## Advanced Topics

{Dive deeper into complex scenarios, edge cases, or advanced features.}

### {Advanced Topic 1}

{Detailed explanation}

```csharp
// More complex example
public class {ExampleClass}
{
    public async Task {ExampleMethod}()
    {
        // Implementation
    }
}
```

### {Advanced Topic 2}

{Detailed explanation}

## Examples

{Real-world examples showing complete implementations.}

### Example: {Scenario}

{Context for the example}

```csharp
// Complete, runnable example
namespace BookStore.{Domain};

public record {Command}(Guid {Entity}Id, {PropertyType} {Property})
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
```

## Best Practices

{List recommended patterns and anti-patterns}

✅ **Do:**
- {Best practice 1}
- {Best practice 2}

❌ **Don't:**
- {Anti-pattern 1}
- {Anti-pattern 2}

## Troubleshooting

{Common issues and solutions}

### Issue: {Problem Description}

**Symptoms:**
- {Symptom 1}
- {Symptom 2}

**Solution:**
{Step-by-step resolution}

### Issue: {Another Problem}

**Symptoms:**
- {Symptom}

**Solution:**
{Resolution}

## Performance Considerations

{If applicable, discuss performance implications and optimization strategies}

> [!TIP]
> {Performance tip or best practice}

## Testing

{Guidance on how to test implementations following this guide}

```csharp
[Test]
public async Task {TestName}()
{
    // Arrange
    var {variable} = new {Type}(...);
    
    // Act
    var result = await {method}({variable});
    
    // Assert
    await Assert.That(result).IsNotNull();
}
```

## Related Documentation

- [{Related Guide 1}]({related-guide-1}.md) - {Brief description}
- [{Related Guide 2}]({related-guide-2}.md) - {Brief description}
- [Official {External Library} Docs]({url}) - {Brief description}

## Further Reading

{Optional: Links to external resources, blog posts, or advanced topics}

- [{Resource Title}]({url})
- [{Resource Title}]({url})
