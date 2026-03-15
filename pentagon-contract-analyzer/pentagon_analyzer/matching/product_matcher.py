"""Match CLINs to commercial products via DigiKey, Mouser, scraper, or CSV."""

from __future__ import annotations

import csv
import logging
from difflib import SequenceMatcher
from pathlib import Path
from typing import Any

from pentagon_analyzer.clients.digikey import DigiKeyClient
from pentagon_analyzer.clients.mouser import MouserClient
from pentagon_analyzer.clients.scraper import PriceScraper
from pentagon_analyzer.matching.clin_parser import clean_description
from pentagon_analyzer.models.contract import CLIN
from pentagon_analyzer.models.pricing import CommercialMatch, CommercialPrice

logger = logging.getLogger(__name__)


class ProductMatcher:
    """Match government CLINs to commercial product equivalents.

    Priority chain: DigiKey API -> Mouser API -> Scraper -> CSV import.
    """

    def __init__(
        self,
        digikey: DigiKeyClient | None = None,
        mouser: MouserClient | None = None,
        scraper: PriceScraper | None = None,
        csv_prices: dict[str, CommercialPrice] | None = None,
    ):
        self._digikey = digikey
        self._mouser = mouser
        self._scraper = scraper or PriceScraper()
        self._csv_prices = csv_prices or {}

    def find_matches(self, clin: CLIN) -> list[CommercialMatch]:
        """Search all available sources for commercial equivalents."""
        query = clean_description(clin.description)
        if not query or len(query) < 3:
            return []

        matches: list[CommercialMatch] = []

        # 1. DigiKey API
        if self._digikey:
            matches.extend(self._search_digikey(clin, query))

        # 2. Mouser API
        if self._mouser:
            matches.extend(self._search_mouser(clin, query))

        # 3. Scraper fallback
        if not matches:
            matches.extend(self._search_scraper(clin, query))

        # 4. CSV import
        if not matches and self._csv_prices:
            matches.extend(self._search_csv(clin, query))

        # Sort by confidence descending, then price ascending
        matches.sort(key=lambda m: (-m.confidence, m.commercial_unit_price))
        return matches

    def best_match(self, clin: CLIN) -> CommercialMatch | None:
        """Return the best commercial match for a CLIN.

        Selects the lowest-priced match with confidence >= 0.3.
        """
        matches = self.find_matches(clin)
        viable = [m for m in matches if m.confidence >= 0.3]
        if not viable:
            return None
        # Among viable matches, pick lowest price
        return min(viable, key=lambda m: m.commercial_unit_price)

    def _search_digikey(self, clin: CLIN, query: str) -> list[CommercialMatch]:
        """Search DigiKey API and convert results to matches."""
        try:
            products = self._digikey.search_products(query, limit=10)
            return [
                self._to_match(clin, query, self._digikey.parse_price(p))
                for p in products
                if p.get("UnitPrice", 0) > 0
            ]
        except Exception as exc:
            logger.warning("DigiKey search failed for '%s': %s", query, exc)
            return []

    def _search_mouser(self, clin: CLIN, query: str) -> list[CommercialMatch]:
        """Search Mouser API and convert results to matches."""
        try:
            parts = self._mouser.search_by_keyword(query, records=10)
            return [
                self._to_match(clin, query, self._mouser.parse_price(p))
                for p in parts
                if p.get("PriceBreaks")
            ]
        except Exception as exc:
            logger.warning("Mouser search failed for '%s': %s", query, exc)
            return []

    def _search_scraper(self, clin: CLIN, query: str) -> list[CommercialMatch]:
        """Search via web scraping fallback."""
        matches = []
        for price in self._scraper.search_digikey(query):
            matches.append(self._to_match(clin, query, price))
        for price in self._scraper.search_mouser(query):
            matches.append(self._to_match(clin, query, price))
        return matches

    def _search_csv(self, clin: CLIN, query: str) -> list[CommercialMatch]:
        """Search pre-loaded CSV pricing data."""
        matches = []
        query_lower = query.lower()
        for key, price in self._csv_prices.items():
            sim = _similarity(query_lower, key.lower())
            if sim >= 0.3:
                match = CommercialMatch(
                    clin_number=clin.clin_number,
                    clin_description=clin.description,
                    matched_part_number=price.part_number,
                    manufacturer=price.manufacturer,
                    commercial_unit_price=price.unit_price,
                    source="csv",
                    confidence=sim,
                    search_query=query,
                    url="",
                )
                matches.append(match)
        return matches

    def _to_match(
        self, clin: CLIN, query: str, price: CommercialPrice
    ) -> CommercialMatch:
        """Convert a CommercialPrice to a CommercialMatch with confidence."""
        confidence = _similarity(
            clean_description(clin.description).lower(),
            price.description.lower(),
        )
        return CommercialMatch(
            clin_number=clin.clin_number,
            clin_description=clin.description,
            matched_part_number=price.part_number,
            manufacturer=price.manufacturer,
            commercial_unit_price=price.unit_price,
            source=price.source,
            confidence=confidence,
            search_query=query,
            url=price.url,
        )


def load_csv_prices(csv_path: Path) -> dict[str, CommercialPrice]:
    """Load commercial pricing from a CSV file.

    Expected columns: part_number, manufacturer, description, unit_price
    Optional: currency, url, quantity_available
    """
    prices: dict[str, CommercialPrice] = {}
    with open(csv_path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            desc = row.get("description", "")
            price = CommercialPrice(
                part_number=row.get("part_number", ""),
                manufacturer=row.get("manufacturer", ""),
                description=desc,
                unit_price=float(row.get("unit_price", 0)),
                currency=row.get("currency", "USD"),
                source="csv",
                url=row.get("url", ""),
                quantity_available=int(row.get("quantity_available", 0)),
                price_breaks=[],
            )
            # Key by description for fuzzy matching
            if desc:
                prices[desc] = price
            if price.part_number:
                prices[price.part_number] = price
    return prices


def _similarity(a: str, b: str) -> float:
    """Compute string similarity ratio (0.0 - 1.0)."""
    if not a or not b:
        return 0.0
    return SequenceMatcher(None, a, b).ratio()
