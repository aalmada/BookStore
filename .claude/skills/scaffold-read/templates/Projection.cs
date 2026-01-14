public class {Resource}Projection : SingleStreamProjection<{Resource}>
{
    public static {Resource} Create({Event} @event) => new(@event.Id, @event.Name);

    // public {Resource} Apply({Event} @event, {Resource} current) => current with { ... };
}
