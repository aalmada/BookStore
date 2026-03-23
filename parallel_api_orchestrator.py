"""
Parallel API Orchestrator
Calls multiple APIs concurrently using asyncio + aiohttp.
"""

import asyncio
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
    timeout: float = 10.0


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
        timeout = aiohttp.ClientTimeout(total=request.timeout)
        async with session.request(
            method=request.method,
            url=request.url,
            headers=request.headers,
            json=request.payload,
            timeout=timeout,
        ) as resp:
            data = await resp.json(content_type=None)
            return ApiResult(
                name=request.name,
                status=resp.status,
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


async def orchestrate(requests: list[ApiRequest]) -> list[ApiResult]:
    """Fire all requests in parallel and return results in the same order."""
    async with aiohttp.ClientSession() as session:
        tasks = [fetch(session, req) for req in requests]
        return await asyncio.gather(*tasks)


def print_results(results: list[ApiResult]) -> None:
    print(f"\n{'─' * 55}")
    for r in results:
        status_str = str(r.status) if r.status else "ERR"
        icon = "✓" if r.ok else "✗"
        print(f"  {icon}  [{status_str}] {r.name:<20} {r.elapsed_ms:>7.1f} ms")
        if r.error:
            print(f"       Error: {r.error}")
    print(f"{'─' * 55}\n")


async def main() -> None:
    # Example: public APIs that require no auth
    requests = [
        ApiRequest(
            name="JSONPlaceholder post",
            url="https://jsonplaceholder.typicode.com/posts/1",
        ),
        ApiRequest(
            name="JSONPlaceholder users",
            url="https://jsonplaceholder.typicode.com/users/1",
        ),
        ApiRequest(
            name="httpbin GET",
            url="https://httpbin.org/get",
        ),
        ApiRequest(
            name="httpbin POST",
            url="https://httpbin.org/post",
            method="POST",
            payload={"hello": "world"},
        ),
        ApiRequest(
            name="Open Meteo weather",
            url="https://api.open-meteo.com/v1/forecast?latitude=40.71&longitude=-74.01&current_weather=true",
        ),
    ]

    print(f"Firing {len(requests)} requests in parallel…")
    t0 = time.monotonic()
    results = await orchestrate(requests)
    total_ms = (time.monotonic() - t0) * 1000

    print_results(results)
    print(f"Total wall time : {total_ms:.1f} ms")
    print(f"Sum of API times: {sum(r.elapsed_ms for r in results):.1f} ms")

    # Access individual results by name for downstream use
    by_name = {r.name: r for r in results}
    post = by_name["JSONPlaceholder post"]
    if post.ok:
        print(f"\nFirst post title: {post.data.get('title')}")


if __name__ == "__main__":
    asyncio.run(main())
