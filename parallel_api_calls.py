"""
Parallel API orchestrator using asyncio + aiohttp.

Usage:
    python parallel_api_calls.py

Requires:
    pip install aiohttp
"""

import asyncio
import json
import time
from dataclasses import dataclass, field
from typing import Any

import aiohttp


@dataclass
class ApiRequest:
    name: str
    url: str
    method: str = "GET"
    headers: dict[str, str] = field(default_factory=dict)
    payload: dict[str, Any] | None = None


@dataclass
class ApiResult:
    name: str
    status: int | None
    data: Any
    elapsed_ms: float
    error: str | None = None

    @property
    def ok(self) -> bool:
        return self.error is None and self.status is not None and self.status < 400


async def fetch(session: aiohttp.ClientSession, request: ApiRequest) -> ApiResult:
    start = time.monotonic()
    try:
        async with session.request(
            method=request.method,
            url=request.url,
            headers=request.headers,
            json=request.payload,
        ) as response:
            data = await response.json(content_type=None)
            return ApiResult(
                name=request.name,
                status=response.status,
                data=data,
                elapsed_ms=(time.monotonic() - start) * 1000,
            )
    except Exception as exc:
        return ApiResult(
            name=request.name,
            status=None,
            data=None,
            elapsed_ms=(time.monotonic() - start) * 1000,
            error=str(exc),
        )


async def orchestrate(
    requests: list[ApiRequest],
    timeout_seconds: float = 10.0,
    max_concurrent: int = 10,
) -> list[ApiResult]:
    """Fetch all requests in parallel, bounded by a semaphore."""
    timeout = aiohttp.ClientTimeout(total=timeout_seconds)
    semaphore = asyncio.Semaphore(max_concurrent)

    async def bounded_fetch(session: aiohttp.ClientSession, req: ApiRequest) -> ApiResult:
        async with semaphore:
            return await fetch(session, req)

    async with aiohttp.ClientSession(timeout=timeout) as session:
        tasks = [bounded_fetch(session, req) for req in requests]
        return await asyncio.gather(*tasks)


def print_results(results: list[ApiResult]) -> None:
    print(f"\n{'=' * 60}")
    print(f"{'NAME':<20} {'STATUS':<8} {'ELAPSED':>10}  RESULT")
    print(f"{'=' * 60}")
    for r in results:
        status = str(r.status) if r.status else "ERR"
        summary = r.error if r.error else json.dumps(r.data)[:60]
        print(f"{r.name:<20} {status:<8} {r.elapsed_ms:>8.0f}ms  {summary}")
    print(f"{'=' * 60}")
    ok = sum(1 for r in results if r.ok)
    print(f"\n{ok}/{len(results)} requests succeeded.\n")


# ---------------------------------------------------------------------------
# Example usage — swap in your real endpoints and auth headers
# ---------------------------------------------------------------------------
REQUESTS = [
    ApiRequest(
        name="posts",
        url="https://jsonplaceholder.typicode.com/posts/1",
    ),
    ApiRequest(
        name="users",
        url="https://jsonplaceholder.typicode.com/users/1",
    ),
    ApiRequest(
        name="todos",
        url="https://jsonplaceholder.typicode.com/todos/1",
    ),
    ApiRequest(
        name="comments",
        url="https://jsonplaceholder.typicode.com/comments/1",
    ),
    ApiRequest(
        name="albums",
        url="https://jsonplaceholder.typicode.com/albums/1",
    ),
    # Example POST — uncomment to test
    # ApiRequest(
    #     name="create-post",
    #     url="https://jsonplaceholder.typicode.com/posts",
    #     method="POST",
    #     headers={"Content-Type": "application/json"},
    #     payload={"title": "foo", "body": "bar", "userId": 1},
    # ),
]


async def main() -> None:
    print(f"Firing {len(REQUESTS)} requests in parallel…")
    wall_start = time.monotonic()

    results = await orchestrate(REQUESTS, timeout_seconds=10.0, max_concurrent=10)

    wall_ms = (time.monotonic() - wall_start) * 1000
    print_results(results)
    print(f"Total wall time: {wall_ms:.0f}ms")


if __name__ == "__main__":
    asyncio.run(main())
