// .NET (C#) — Install and Example

### Installation

**NuGet (project):**

```sh
dotnet add package GitHub.Copilot.SDK
```

**C# file-based script:**

```csharp
#! /usr/bin/env dotnet-script
#r "nuget: GitHub.Copilot.SDK"
```

# Checking if Copilot CLI is installed

Before calling the Copilot CLI from .NET, you can check if it is available in the system path:

```csharp
using System.Diagnostics;
try {
	Process.Start(new ProcessStartInfo {
		FileName = "copilot",
		Arguments = "--version",
		RedirectStandardOutput = true,
		UseShellExecute = false,
		CreateNoWindow = true
	});
} catch {
	throw new Exception("Copilot CLI is not installed or not in PATH");
}
```

# .NET (C#) Example: Basic Usage

```csharp
using GitHub.Copilot.SDK;

await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig { Model = "gpt-4.1" });
var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = "Hello" });
Console.WriteLine(response?.Data.Content);
```
