using System;

namespace BookStore.ApiService.Tests;

// Invalid: Using DateTime.UtcNow
public class InvalidDateTimeUtcNow
{
    public void CreateEvent()
    {
        var now = DateTime.UtcNow;
    }
}
