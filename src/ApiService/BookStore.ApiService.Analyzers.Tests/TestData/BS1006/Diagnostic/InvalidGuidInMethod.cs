using System;

namespace BookStore.ApiService.Tests;

// Invalid: Using Guid.NewGuid() in method
public class InvalidGuidInMethod
{
    public void CreateEntity()
    {
        var id = Guid.NewGuid();
    }
}
