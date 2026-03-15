"""Configuration management - loads API keys and settings from .env."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path

from dotenv import load_dotenv


@dataclass(frozen=True)
class Config:
    """Application configuration loaded from environment variables."""

    # SAM.gov (shared key for Contract Awards + Entity APIs)
    sam_api_key: str = ""

    # DigiKey OAuth2
    digikey_client_id: str = ""
    digikey_client_secret: str = ""

    # Mouser
    mouser_api_key: str = ""

    # Operational
    cache_dir: Path = field(default_factory=lambda: Path("data"))
    output_dir: Path = field(default_factory=lambda: Path("data/output"))
    markup_threshold: float = 10.0
    cache_ttl_hours: int = 168  # 7 days
    max_retries: int = 5
    log_level: str = "INFO"

    @classmethod
    def from_env(cls, dotenv_path: str | None = None) -> Config:
        """Load configuration from .env file and environment variables."""
        load_dotenv(dotenv_path)
        return cls(
            sam_api_key=os.getenv("SAM_API_KEY", ""),
            digikey_client_id=os.getenv("DIGIKEY_CLIENT_ID", ""),
            digikey_client_secret=os.getenv("DIGIKEY_CLIENT_SECRET", ""),
            mouser_api_key=os.getenv("MOUSER_API_KEY", ""),
            cache_dir=Path(os.getenv("CACHE_DIR", "data")),
            output_dir=Path(os.getenv("OUTPUT_DIR", "data/output")),
            markup_threshold=float(os.getenv("MARKUP_THRESHOLD", "10.0")),
            cache_ttl_hours=int(os.getenv("CACHE_TTL_HOURS", "168")),
            max_retries=int(os.getenv("MAX_RETRIES", "5")),
            log_level=os.getenv("LOG_LEVEL", "INFO"),
        )

    @property
    def has_digikey(self) -> bool:
        return bool(self.digikey_client_id and self.digikey_client_secret)

    @property
    def has_mouser(self) -> bool:
        return bool(self.mouser_api_key)

    @property
    def has_sam(self) -> bool:
        return bool(self.sam_api_key)
