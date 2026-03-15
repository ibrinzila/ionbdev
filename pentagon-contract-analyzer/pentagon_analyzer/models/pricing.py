"""Commercial pricing data models."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class CommercialPrice:
    """A commercial product price from a distributor."""

    part_number: str
    manufacturer: str
    description: str
    unit_price: float
    currency: str
    source: str  # "digikey", "mouser", "scraper", "csv"
    url: str
    quantity_available: int
    price_breaks: list[dict]  # [{quantity: int, unit_price: float}, ...]


@dataclass
class CommercialMatch:
    """A CLIN matched to a commercial product."""

    clin_number: str
    clin_description: str
    matched_part_number: str
    manufacturer: str
    commercial_unit_price: float
    source: str  # "digikey", "mouser", "scraper", "csv"
    confidence: float  # 0.0 - 1.0 match confidence
    search_query: str  # cleaned query used for matching
    url: str
