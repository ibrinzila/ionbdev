"""Rate limiter with token bucket and exponential backoff."""

from __future__ import annotations

import time
import threading


class RateLimiter:
    """Token bucket rate limiter - thread-safe."""

    def __init__(self, requests_per_second: float = 2.0):
        self._rate = requests_per_second
        self._tokens = requests_per_second
        self._max_tokens = requests_per_second
        self._last_refill = time.monotonic()
        self._lock = threading.Lock()

    def acquire(self) -> None:
        """Block until a token is available."""
        while True:
            with self._lock:
                self._refill()
                if self._tokens >= 1.0:
                    self._tokens -= 1.0
                    return
            time.sleep(0.1)

    def _refill(self) -> None:
        now = time.monotonic()
        elapsed = now - self._last_refill
        self._tokens = min(self._max_tokens, self._tokens + elapsed * self._rate)
        self._last_refill = now


def backoff_seconds(attempt: int, retry_after: str | None = None) -> float:
    """Calculate backoff delay for a given retry attempt."""
    if retry_after and retry_after.isdigit():
        return float(retry_after)
    return min(2 ** (attempt + 1), 120)
