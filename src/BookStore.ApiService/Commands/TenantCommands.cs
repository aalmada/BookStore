namespace BookStore.ApiService.Commands;

public record CreateTenantCommand(string Id, string Name, bool IsEnabled = true);
public record UpdateTenantCommand(string Name, bool IsEnabled);
