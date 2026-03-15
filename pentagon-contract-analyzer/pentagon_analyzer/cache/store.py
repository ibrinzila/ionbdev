"""SQLite-backed response cache with TTL expiration."""

from __future__ import annotations

import json
import sqlite3
import time
from pathlib import Path
from typing import Any


class CacheStore:
    """Persistent key-value cache backed by SQLite."""

    def __init__(self, db_path: Path, ttl_hours: int = 168):
        self._db_path = db_path
        self._ttl_seconds = ttl_hours * 3600
        db_path.parent.mkdir(parents=True, exist_ok=True)
        self._conn = sqlite3.connect(str(db_path))
        self._conn.execute(
            "CREATE TABLE IF NOT EXISTS cache ("
            "  key TEXT PRIMARY KEY,"
            "  value TEXT NOT NULL,"
            "  created_at REAL NOT NULL"
            ")"
        )
        self._conn.commit()

    def get(self, key: str) -> dict[str, Any] | None:
        """Retrieve cached value if not expired."""
        row = self._conn.execute(
            "SELECT value, created_at FROM cache WHERE key = ?", (key,)
        ).fetchone()
        if row is None:
            return None
        value, created_at = row
        if time.time() - created_at > self._ttl_seconds:
            self._conn.execute("DELETE FROM cache WHERE key = ?", (key,))
            self._conn.commit()
            return None
        return json.loads(value)

    def put(self, key: str, data: dict[str, Any]) -> None:
        """Store a value in the cache."""
        self._conn.execute(
            "INSERT OR REPLACE INTO cache (key, value, created_at) VALUES (?, ?, ?)",
            (key, json.dumps(data), time.time()),
        )
        self._conn.commit()

    def clear(self) -> int:
        """Remove all entries. Returns count deleted."""
        cursor = self._conn.execute("DELETE FROM cache")
        self._conn.commit()
        return cursor.rowcount

    def clear_expired(self) -> int:
        """Remove only expired entries. Returns count deleted."""
        cutoff = time.time() - self._ttl_seconds
        cursor = self._conn.execute(
            "DELETE FROM cache WHERE created_at < ?", (cutoff,)
        )
        self._conn.commit()
        return cursor.rowcount

    def close(self) -> None:
        self._conn.close()
