"""Pytest configuration for copilot-skill-creator tests.

The `copilot` SDK package is not required to be installed for the test suite
to run. All tests mock CopilotClient and _call_copilot directly, so we only
need the import to succeed. We register a lightweight stub module before any
test module is imported.
"""

import sys
import types
from unittest.mock import AsyncMock, MagicMock


def _make_copilot_stub():
    """Return a stub module that satisfies 'from copilot import CopilotClient, PermissionHandler'."""
    mod = types.ModuleType("copilot")

    class PermissionHandler:
        approve_all = staticmethod(lambda *a, **kw: None)

    class SubprocessConfig:
        pass

    class CopilotClient:
        async def start(self):
            pass

        async def stop(self):
            pass

        async def create_session(self, config=None):
            session = MagicMock()
            session.__aenter__ = AsyncMock(return_value=session)
            session.__aexit__ = AsyncMock(return_value=False)
            session.on = MagicMock()
            session.send = AsyncMock()
            return session

    mod.CopilotClient = CopilotClient
    mod.PermissionHandler = PermissionHandler
    mod.SubprocessConfig = SubprocessConfig
    return mod


# Register the stub if the real package is not installed
if "copilot" not in sys.modules:
    try:
        import copilot  # noqa: F401
    except ModuleNotFoundError:
        sys.modules["copilot"] = _make_copilot_stub()
