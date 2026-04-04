
# Aspire AppHost: C# and TypeScript

Aspire supports both **C#** and **TypeScript** AppHosts for orchestrating distributed applications. Both are first-class options—choose based on your stack:

- **C# AppHost**: For .NET/C#-centric solutions
- **TypeScript AppHost**: For Node.js/React/JS-centric solutions

See official docs:
- [C# AppHost Quickstart](https://aspire.dev/get-started/first-app/)
- [TypeScript AppHost Quickstart](https://aspire.dev/get-started/first-app-typescript-apphost/)

---

## Create a New Aspire App

| Language     | Command Example                                                                 |
|--------------|---------------------------------------------------------------------------------|
| **C#**       | `aspire new aspire-starter -n aspire-app -o aspire-app`                         |
| **TypeScript** | `aspire new aspire-ts-starter -n aspire-app -o aspire-app`                      |

Both commands prompt to configure AI agent environments (select 'y'). Each generates a solution with API, frontend, and AppHost in the respective language.

---

## AppHost Example

### C#
```csharp
var builder = DistributedApplication.CreateBuilder(args);
var api = builder.AddProject("api", "./api/Api.csproj").WithHttpEndpoint(env: "PORT");
var frontend = builder.AddProject("frontend", "./frontend/Frontend.csproj").WithReference(api).WaitFor(api);
builder.Build().Run();
```

### TypeScript
```typescript
import { createBuilder } from './.modules/aspire.js';
const builder = await createBuilder();
const app = await builder.addNodeApp("app", "./api", "src/index.ts").withHttpEndpoint({ env: "PORT" }).withExternalHttpEndpoints();
const frontend = await builder.addViteApp("frontend", "./frontend").withReference(app).waitFor(app);
await app.publishWithContainerFiles(frontend, "./static");
await builder.build().run();
```

---

## Run the App

```bash
cd ./aspire-app
aspire run
# Dashboard URL appears in terminal
```

---

**Note:** Aspire treats C# and TypeScript AppHosts as equal options. Use the one that best fits your technology stack.

