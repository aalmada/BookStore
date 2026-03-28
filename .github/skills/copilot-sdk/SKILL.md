
---
name: copilot-sdk
description: >
  Use this skill when writing scripts that call the GitHub Copilot SDK directly—including setting
  up a client, creating sessions, sending prompts, streaming responses, defining tools, using hooks,
  and handling permissions. Also triggers for: installing the SDK, troubleshooting session errors,
  using create_session, working with PermissionHandler, defining on_pre_tool_use hooks, or
  understanding the SDK's async API. Use when a deterministic script needs to delegate
  non-deterministic tasks (classification, extraction, synthesis, evaluation) to an LLM.
  DO NOT USE FOR: building GitHub App Copilot extensions (copilot_chat.completion endpoint),
  calling other LLM providers directly (OpenAI, Anthropic, Azure OpenAI, Ollama, LangChain),
  or querying the GitHub Copilot REST/metrics API.
---



# Installation / Adding the Copilot SDK

The Copilot SDK must be installed or added to your project before use in any language. See the install instructions for your language:

- [Python: install & example](examples/python-basic.md)
- [Go: install & example](examples/go-basic.md)
- [Node.js: install & example](examples/nodejs-basic.md)
- [.NET: install & example](examples/dotnet-basic.md)

# GitHub Copilot SDK — Scripting Guide

## When to Use the SDK

Use the `github-copilot-sdk` whenever a **deterministic script** needs to delegate
**non-deterministic tasks** to an LLM — things that cannot be solved with pure logic:

| Non-deterministic task | Example |
|---|---|
| Classification | "Is this commit message a bug fix or a feature?" |
| Extraction | "Pull the action items from this meeting transcript" |
| Synthesis / summarisation | "Summarise these 50 test failures into themes" |
| Evaluation / scoring | "Grade this answer against the rubric" |
| Free-form generation | "Write a changelog entry from these commits" |

The rest of the script stays fully deterministic — file I/O, loops, conditionals,
formatting — while the SDK handles the parts that require judgment.

> **CLI dependency:** The SDK does **not** make direct API calls. It spawns and drives
> the **GitHub Copilot CLI** as a subprocess. The CLI must be installed and authenticated
> before running any SDK script (see [Prerequisites](#prerequisites) below).

## Python Version Requirement

**Python 3.10 or later is required.** The SDK uses union type syntax (`X | Y`, `dict | None`)
that is a syntax error on Python 3.9 and earlier.

Check your version:
```bash
> source .venv/bin/activate
> pip install github-copilot-sdk
import asyncio

# GitHub Copilot SDK — Scripting Guide

## When to Use the SDK

Use the Copilot SDK in any deterministic script that needs to delegate non-deterministic tasks (classification, extraction, summarization, evaluation, free-form generation) to an LLM. The SDK is available for Python, Go, .NET (C#), and Node.js (JavaScript/TypeScript).

> **CLI dependency:** The SDK does **not** make direct API calls. It spawns and drives the **GitHub Copilot CLI** as a subprocess. The CLI must be installed and authenticated before running any SDK script. See the [official installation guide](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli).


## Language Quick Start Examples

- [Python Example](examples/python-basic.md)
- [Go Example](examples/go-basic.md)
- [Node.js Example](examples/nodejs-basic.md)
- [.NET (C#) Example](examples/dotnet-basic.md)

Each example demonstrates how to create a client, start a session, send a prompt, and handle the response. See the referenced files for details and advanced usage.

## Documentation and Advanced Usage

- [Python SDK README](https://raw.githubusercontent.com/github/copilot-sdk/refs/heads/main/python/README.md)
- [Go SDK README](https://raw.githubusercontent.com/github/copilot-sdk/refs/heads/main/go/README.md)
- [Node.js SDK README](https://raw.githubusercontent.com/github/copilot-sdk/refs/heads/main/nodejs/README.md)
- [.NET SDK README](https://raw.githubusercontent.com/github/copilot-sdk/refs/heads/main/dotnet/README.md)

For .NET file-based scripting, see [Microsoft Learn: File-based programs in C#](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/file-based-programs).

## Key Points

- The SDK requires the Copilot CLI to be installed and authenticated.
- Each language SDK provides async or event-driven APIs for session management and prompt handling.
- Permission handlers are required for tool execution (see each language's example for details).
- Advanced features (custom tools, streaming, system message customization, telemetry, etc.) are documented in the official SDK READMEs above.

## Example Directory

All code examples have been moved to the `examples/` subdirectory for clarity and multi-language support.

---

| Parameter | Type | Description |
|-----------|------|-------------|
| `on_permission_request` | `PermissionHandler \| callable` | **Required.** Handles tool-use permission requests. Use `PermissionHandler.approve_all` to allow everything. |
| `model` | `str \| None` | LLM model name: `"gpt-4.1"`, `"claude-sonnet-4.5"`, etc. |
| `system_message` | `dict \| None` | Override system prompt: `{"content": "..."}` |
| `hooks` | `dict \| None` | Low-level hooks dict (see Hooks section below) |
| `infinite_sessions` | `dict \| None` | `{"enabled": True/False}` |

## Context Manager vs Manual Session

`create_session` returns an async context manager. Prefer `async with`:
```python
async with await client.create_session(**config) as session:
    await session.send("Hello")
# session is automatically closed here
```

Or manage manually:
```python
session = await client.create_session(**config)
await session.send("Hello")
await session.close()
```

## Event Handling

Events are dispatched via `session.on(callback)`. Key event types (access via `event.type.value`):

| Event type string | When fired |
|---|---|
| `"assistant.message"` | Full response text is ready (`event.data.content`) |
| `"assistant.message_delta"` | Streaming chunk (`event.data.delta_content`) |
| `"assistant.usage"` | LLM API call completed — token counts and cost data (`event.data.input_tokens`, `event.data.output_tokens`, `event.data.cache_read_tokens`, `event.data.cache_write_tokens`, `event.data.cost`) |
| `"session.idle"` | No active processing — safe to proceed |
| `"session.shutdown"` | Session ended — `event.data.model_metrics` contains per-model aggregated usage (`Usage.input_tokens`, `Usage.output_tokens`, `Usage.cache_read_tokens`, `Usage.cache_write_tokens`) |
| `"tool.execution_start"` | Tool invocation started |
| `"tool.execution_complete"` | Tool execution finished |
| `"session.error"` | Error occurred |

```python
done = asyncio.Event()
result: list[str] = []

def on_event(event):
    if event.type.value == "assistant.message":
        result.append(event.data.content)
        done.set()
    elif event.type.value == "session.idle":
        done.set()

session.on(on_event)
await session.send("Your prompt here")
await done.wait()
response_text = result[0] if result else ""
```

## Token Usage Tracking

Use the `assistant.usage` event to accumulate real LLM token counts across an entire
session. It fires once per API call (including sub-agent calls) and carries
per-request token data.

```python
input_tokens = 0
output_tokens = 0

def on_event(event):
    if event.type.value == "assistant.message":
        result.append(event.data.content)
    elif event.type.value == "assistant.usage":
        nonlocal input_tokens, output_tokens
        input_tokens += int(event.data.input_tokens or 0)
        output_tokens += int(event.data.output_tokens or 0)
    elif event.type.value == "session.idle":
        done.set()
```

**Fields on `event.data` for `assistant.usage`:**

| Field | Type | Description |
|---|---|---|
| `input_tokens` | `float \| None` | Input tokens consumed by this API call |
| `output_tokens` | `float \| None` | Output tokens produced by this API call |
| `cache_read_tokens` | `float \| None` | Tokens read from prompt cache |
| `cache_write_tokens` | `float \| None` | Tokens written to prompt cache |
| `cost` | `float \| None` | Model multiplier cost for billing |
| `model` | `str \| None` | Model used for this API call |
| `parent_tool_call_id` | `str \| None` | Set when the call originated from a sub-agent |
| `copilot_usage` | `CopilotUsage \| None` | Itemised billing data (`token_details`, `total_nano_aiu`) |

**`session.shutdown` carries aggregated per-model totals** via `event.data.model_metrics`
(a `dict[str, ModelMetric]` keyed by model identifier). Each `ModelMetric` has:
- `usage.input_tokens` / `usage.output_tokens` / `usage.cache_read_tokens` / `usage.cache_write_tokens` — cumulative totals across all requests to that model
- `requests.count` / `requests.cost` — request count and cumulative billing cost

## Streaming

```python
import sys

def on_event(event):
    if event.type.value == "assistant.message_delta":
        sys.stdout.write(event.data.delta_content)
        sys.stdout.flush()
    elif event.type.value == "session.idle":
        print()  # newline after stream ends

session.on(on_event)
await session.send("Tell me a short story")
```

## Using `send_and_wait`

`send_and_wait` is a convenience method that sends a message and waits for the session
to reach idle before returning:

```python
response = await session.send_and_wait({"prompt": "What is 2 + 2?"})
print(response.data.content)
```

## Hooks: `on_pre_tool_use`

Hooks let you intercept and inspect tool calls before they execute. Useful for:
- Detecting whether a specific skill/file was triggered
- Logging tool calls
- Modifying or blocking tool arguments

```python
async def on_pre_tool_use(input_data: dict, invocation) -> dict:
    tool_args = input_data.get("toolArgs", {})
    print(f"Tool about to be called with args: {tool_args}")
    # Return allow decision with (optionally modified) args
    return {"permissionDecision": "allow", "modifiedArgs": tool_args}

session_config = {
    "on_permission_request": PermissionHandler.approve_all,
    "hooks": {"on_pre_tool_use": on_pre_tool_use},
    "infinite_sessions": {"enabled": False},
}
async with await client.create_session(**session_config) as session:
    ...
```

> Note: `on_permission_request` and `hooks.on_pre_tool_use` serve different purposes.
> `on_permission_request` grants/denies tool use at a higher level.
> `on_pre_tool_use` fires right before execution and can inspect/modify args.

## Custom Tools (Pydantic)

```python
import asyncio
from copilot import CopilotClient, PermissionHandler
from copilot.tools import define_tool
from pydantic import BaseModel, Field

class GetWeatherParams(BaseModel):
    city: str = Field(description="The name of the city")

@define_tool(description="Get the current weather for a city")
async def get_weather(params: GetWeatherParams) -> dict:
    return {"city": params.city, "temperature": "72°F", "condition": "sunny"}

async def main():
    client = CopilotClient()
    await client.start()
    try:
        async with await client.create_session(
            on_permission_request=PermissionHandler.approve_all,
            model="gpt-4.1",
            tools=[get_weather],
            infinite_sessions={"enabled": False},
        ) as session:
            done = asyncio.Event()
            result: list[str] = []

            def on_event(event):
                if event.type.value == "assistant.message":
                    result.append(event.data.content)
                    done.set()
                elif event.type.value == "session.idle":
                    done.set()

            session.on(on_event)
            await session.send("What's the weather in Paris?")
            await done.wait()
        print(result[0] if result else "")
    finally:
        await client.stop()

asyncio.run(main())
```

## System Message

```python
async with await client.create_session(
    on_permission_request=PermissionHandler.approve_all,
    system_message={"content": "You are a concise assistant. Reply in one sentence."},
    infinite_sessions={"enabled": False},
) as session:
    ...
```

## MCP Server Integration

```python
async with await client.create_session(
    on_permission_request=PermissionHandler.approve_all,
    model="gpt-4.1",
    mcp_servers={
        "github": {
            "type": "http",
            "url": "https://api.githubcopilot.com/mcp/",
        },
    },
) as session:
    ...
```

## External CLI Server

```bash
# Terminal 1: start CLI in server mode
copilot --server --port 4321
```

```python
# Connect SDK to existing server
client = CopilotClient({"cli_url": "localhost:4321"})
await client.start()
# SDK will NOT spawn a new CLI process
```

## Error Handling Pattern

```python
import asyncio
from copilot import CopilotClient, PermissionHandler

async def call_copilot(prompt: str, model: str) -> str:
    config = {
        "on_permission_request": PermissionHandler.approve_all,
        "infinite_sessions": {"enabled": False},
        "model": model,
    }
    client = CopilotClient()
    await client.start()
    try:
        async with await client.create_session(**config) as session:
            result: list[str] = []
            done = asyncio.Event()

            def on_event(event):
                if event.type.value == "assistant.message":
                    result.append(event.data.content)
                    done.set()
                elif event.type.value == "session.idle":
                    done.set()

            session.on(on_event)
            await session.send(prompt)
            await asyncio.wait_for(done.wait(), timeout=30)

        return result[0] if result else ""
    except asyncio.TimeoutError:
        return ""  # or raise
    finally:
        await client.stop()
```

## Script Boilerplate

```python
#!/usr/bin/env python3
"""Description of what this script does."""

from __future__ import annotations  # needed for forward references on Python <3.12

import asyncio
from copilot import CopilotClient, PermissionHandler


async def main() -> None:
    client = CopilotClient()
    await client.start()
    try:
        async with await client.create_session(
            on_permission_request=PermissionHandler.approve_all,
            infinite_sessions={"enabled": False},
            model="gpt-4.1",
        ) as session:
            done = asyncio.Event()
            result: list[str] = []

            def on_event(event):
                if event.type.value == "assistant.message":
                    result.append(event.data.content)
                    done.set()
                elif event.type.value == "session.idle":
                    done.set()

            session.on(on_event)
            await session.send("Your prompt here")
            await done.wait()

        print(result[0] if result else "")
    finally:
        await client.stop()


if __name__ == "__main__":
    asyncio.run(main())
```

## Other Languages (Quick Reference)

### Node.js / TypeScript
```bash
npm install @github/copilot-sdk tsx
```
```typescript
import { CopilotClient } from "@github/copilot-sdk";
const client = new CopilotClient();
const session = await client.createSession({ model: "gpt-4.1" });
const response = await session.sendAndWait({ prompt: "Hello" });
console.log(response?.data.content);
await client.stop();
```

### Go
```bash
go get github.com/github/copilot-sdk/go
```
```go
client := copilot.NewClient(nil)
client.Start()
defer client.Stop()
session, _ := client.CreateSession(&copilot.SessionConfig{Model: "gpt-4.1"})
response, _ := session.SendAndWait(copilot.MessageOptions{Prompt: "Hello"}, 0)
fmt.Println(*response.Data.Content)
```

### .NET (C#)
```bash
dotnet add package GitHub.Copilot.SDK
```
```csharp
await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig { Model = "gpt-4.1" });
var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = "Hello" });
Console.WriteLine(response?.Data.Content);
```

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| SDK fails to start with "CLI not found" | Install + authenticate the Copilot CLI first (`gh copilot --version`) |
| `pip install copilot-sdk` | Package name is `github-copilot-sdk` |
| `create_session(config_dict)` positional | Use `create_session(**config_dict)` |
| Running on Python 3.9 | Upgrade to Python 3.10+; use a venv |
| `pip install` fails with PEP 668 error | Use `python3.12 -m venv .venv && source .venv/bin/activate` |
| Accessing `event.type == "assistant.message"` | Use `event.type.value == "assistant.message"` |
| Script exits before response arrives | Add `asyncio.Event` + `await done.wait()` |
