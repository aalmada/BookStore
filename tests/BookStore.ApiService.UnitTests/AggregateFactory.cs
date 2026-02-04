using System.Reflection;

namespace BookStore.ApiService.UnitTests;

/// <summary>
/// Domain-neutral factory to create and hydrate aggregates for testing purposes.
/// This mimics how Marten rehydrates aggregates from the event stream.
/// </summary>
public static class AggregateFactory
{
    public static T Hydrate<T>(params object[] events) where T : class
    {
        var aggregate = (T)Activator.CreateInstance(typeof(T), true)!;
        var type = typeof(T);

        foreach (var @event in events)
        {
            // Find the appropriate Apply method for this event type
            var applyMethod = type.GetMethod("Apply",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                [@event.GetType()]);

            if (applyMethod != null)
            {
                _ = applyMethod.Invoke(aggregate, [@event]);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Aggregate {type.Name} does not have an Apply method for event type {@event.GetType().Name}");
            }
        }

        return aggregate;
    }
}
