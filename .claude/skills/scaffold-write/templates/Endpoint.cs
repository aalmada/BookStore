public static async Task<IResult> {Action}{Resource}(
    [FromBody] {RequestDto} request,
    [FromServices] IMessageBus bus,
    CancellationToken cancellationToken)
{
    var cmd = new {CommandName}(...);
    await bus.InvokeAsync(cmd, cancellationToken);
    return Results.Ok();
}
