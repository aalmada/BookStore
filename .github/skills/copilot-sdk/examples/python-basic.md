# Python — Install and Example

## Installation

```sh
pip install copilot-sdk
```

# Checking if Copilot CLI is installed

Before calling the Copilot CLI from Python, you can check if it is available in the system path:

```python
import shutil
if shutil.which("copilot") is None:
    raise RuntimeError("Copilot CLI is not installed or not in PATH")
```

# Python Example: Basic Usage

```python
import asyncio
from copilot import CopilotClient, PermissionHandler

async def main():
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
            await session.send("What is 2 + 2?")
            await done.wait()

        print(result[0] if result else "")
    finally:
        await client.stop()

asyncio.run(main())
```
