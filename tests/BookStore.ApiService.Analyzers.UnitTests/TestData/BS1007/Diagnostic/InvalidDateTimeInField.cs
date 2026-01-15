using System;

namespace BookStore.ApiService.Tests;

// Invalid: Using DateTime.Now in field initializer
public class InvalidDateTimeInField
{
    private DateTime _timestamp = DateTime.Now;
}
