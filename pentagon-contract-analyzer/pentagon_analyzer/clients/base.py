"""Base API client with retry, rate-limit backoff, and response caching."""

from __future__ import annotations

import hashlib
import json
import logging
import time
from typing import Any

import requests

from pentagon_analyzer.cache.store import CacheStore
from pentagon_analyzer.utils.rate_limiter import RateLimiter, backoff_seconds

logger = logging.getLogger(__name__)


class BaseAPIClient:
    """Base client all API clients inherit from.

    Provides:
    - SQLite-backed response caching
    - Token-bucket rate limiting
    - Exponential backoff on 429/5xx
    """

    BASE_URL: str = ""  # Override in subclasses

    def __init__(
        self,
        cache: CacheStore,
        max_retries: int = 5,
        rate: float = 2.0,
    ):
        self._session = requests.Session()
        self._cache = cache
        self._max_retries = max_retries
        self._limiter = RateLimiter(rate)

    def _cache_key(
        self,
        method: str,
        url: str,
        params: dict | None,
        body: dict | None,
    ) -> str:
        raw = json.dumps(
            {"m": method, "u": url, "p": params, "b": body}, sort_keys=True
        )
        return hashlib.sha256(raw.encode()).hexdigest()

    def _request(
        self,
        method: str,
        path: str,
        *,
        params: dict | None = None,
        json_body: dict | None = None,
        headers: dict | None = None,
        use_cache: bool = True,
        timeout: int = 30,
    ) -> dict[str, Any]:
        """Execute an HTTP request with caching and retry logic."""
        url = f"{self.BASE_URL.rstrip('/')}/{path.lstrip('/')}"
        key = self._cache_key(method, url, params, json_body)

        if use_cache:
            cached = self._cache.get(key)
            if cached is not None:
                logger.debug("Cache hit: %s %s", method, url)
                return cached

        for attempt in range(self._max_retries):
            self._limiter.acquire()
            try:
                resp = self._session.request(
                    method,
                    url,
                    params=params,
                    json=json_body,
                    headers=headers,
                    timeout=timeout,
                )
                if resp.status_code == 429:
                    wait = backoff_seconds(
                        attempt, resp.headers.get("Retry-After")
                    )
                    logger.warning("Rate limited on %s. Waiting %.1fs", url, wait)
                    time.sleep(wait)
                    continue
                resp.raise_for_status()
                data = resp.json()
                if use_cache:
                    self._cache.put(key, data)
                return data
            except requests.exceptions.RequestException as exc:
                if attempt == self._max_retries - 1:
                    raise
                wait = backoff_seconds(attempt)
                logger.warning(
                    "Request failed (%s), retry %d/%d in %ds",
                    exc,
                    attempt + 1,
                    self._max_retries,
                    wait,
                )
                time.sleep(wait)

        raise RuntimeError(f"Exhausted {self._max_retries} retries for {method} {url}")
