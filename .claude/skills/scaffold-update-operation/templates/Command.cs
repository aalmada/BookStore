namespace BookStore.ApiService.Commands.{Resource};

public record Update{Resource}(Guid Id, string NewName /* other args */);
