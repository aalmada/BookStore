using System;

namespace BookStore.ApiService.Tests;

// Invalid: Using Guid.NewGuid() in field initializer
public class InvalidGuidInField
{
    private Guid _id = Guid.NewGuid();
}
