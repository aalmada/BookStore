query = new ReactiveQuery<T>(
    queryFn: async () => await Client.GetAsync(Id),
    eventsService: EventsService,
    invalidationService: InvalidationService,
    queryKeys: ["{Resource}", "{Resource}:{Id}"], // e.g. "Books", "Book:123"
    onStateChanged: StateHasChanged,
    logger: Logger
);
await query.LoadAsync();
