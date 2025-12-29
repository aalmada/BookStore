using System;

namespace BookStore.ApiService.Tests;

// Invalid: Using DateTime.Now
public class InvalidDateTimeNow
{
    public void CreateEvent()
    {
        var now = DateTime.Now;
    }
}
