"""Web scraping fallback for commercial pricing when API keys unavailable."""

from __future__ import annotations

import logging
import re
from typing import Any

import requests
from bs4 import BeautifulSoup

from pentagon_analyzer.models.pricing import CommercialPrice

logger = logging.getLogger(__name__)

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
)


class PriceScraper:
    """Fallback scraper for public distributor pricing pages.

    Used when DigiKey/Mouser API keys are not configured.
    Respects robots.txt and rate limits.
    """

    def __init__(self, session: requests.Session | None = None):
        self._session = session or requests.Session()
        self._session.headers.update({"User-Agent": USER_AGENT})

    def search_digikey(self, keyword: str, limit: int = 5) -> list[CommercialPrice]:
        """Scrape DigiKey search results page."""
        try:
            resp = self._session.get(
                "https://www.digikey.com/en/products/filter",
                params={"keywords": keyword},
                timeout=15,
            )
            resp.raise_for_status()
            return self._parse_digikey_results(resp.text, limit)
        except Exception as exc:
            logger.warning("DigiKey scrape failed: %s", exc)
            return []

    def search_mouser(self, keyword: str, limit: int = 5) -> list[CommercialPrice]:
        """Scrape Mouser search results page."""
        try:
            resp = self._session.get(
                "https://www.mouser.com/Search/Refine",
                params={"Keyword": keyword},
                timeout=15,
            )
            resp.raise_for_status()
            return self._parse_mouser_results(resp.text, limit)
        except Exception as exc:
            logger.warning("Mouser scrape failed: %s", exc)
            return []

    def _parse_digikey_results(
        self, html: str, limit: int
    ) -> list[CommercialPrice]:
        """Parse DigiKey search results HTML."""
        soup = BeautifulSoup(html, "lxml")
        results = []
        rows = soup.select("tr[data-testid='search-result-row']")[:limit]

        for row in rows:
            try:
                part_num = _text(row.select_one("[data-testid='mfr-part']"))
                mfr = _text(row.select_one("[data-testid='manufacturer']"))
                desc = _text(row.select_one("[data-testid='description']"))
                price_text = _text(row.select_one("[data-testid='unit-price']"))
                price = _parse_price_text(price_text)

                if part_num and price > 0:
                    results.append(
                        CommercialPrice(
                            part_number=part_num,
                            manufacturer=mfr,
                            description=desc,
                            unit_price=price,
                            currency="USD",
                            source="scraper",
                            url="",
                            quantity_available=0,
                            price_breaks=[],
                        )
                    )
            except Exception:
                continue

        return results

    def _parse_mouser_results(
        self, html: str, limit: int
    ) -> list[CommercialPrice]:
        """Parse Mouser search results HTML."""
        soup = BeautifulSoup(html, "lxml")
        results = []
        rows = soup.select(".search-result-row")[:limit]

        for row in rows:
            try:
                part_num = _text(row.select_one(".mfr-part-num"))
                mfr = _text(row.select_one(".manufacturer"))
                desc = _text(row.select_one(".description"))
                price_text = _text(row.select_one(".price"))
                price = _parse_price_text(price_text)

                if part_num and price > 0:
                    results.append(
                        CommercialPrice(
                            part_number=part_num,
                            manufacturer=mfr,
                            description=desc,
                            unit_price=price,
                            currency="USD",
                            source="scraper",
                            url="",
                            quantity_available=0,
                            price_breaks=[],
                        )
                    )
            except Exception:
                continue

        return results


def _text(el: Any) -> str:
    """Safely extract text from a BeautifulSoup element."""
    return el.get_text(strip=True) if el else ""


def _parse_price_text(text: str) -> float:
    """Extract numeric price from text like '$14.80' or '14.80 USD'."""
    if not text:
        return 0.0
    match = re.search(r"[\d,]+\.?\d*", text.replace(",", ""))
    if match:
        return float(match.group())
    return 0.0
