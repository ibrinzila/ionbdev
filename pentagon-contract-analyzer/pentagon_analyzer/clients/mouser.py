"""Mouser API v1 client for commercial electronics pricing."""

from __future__ import annotations

import logging
from typing import Any

from pentagon_analyzer.clients.base import BaseAPIClient
from pentagon_analyzer.models.pricing import CommercialPrice

logger = logging.getLogger(__name__)


class MouserClient(BaseAPIClient):
    """Client for Mouser Part Search API v1.

    Docs: https://www.mouser.com/api-solutions/
    Auth: API key (no expiration).
    """

    BASE_URL = "https://api.mouser.com/api/v1"

    def __init__(self, api_key: str, **kwargs: Any):
        super().__init__(**kwargs)
        self._api_key = api_key

    def search_by_keyword(self, keyword: str, records: int = 50) -> list[dict[str, Any]]:
        """Search Mouser catalog by keyword."""
        data = self._request(
            "POST",
            f"/search/keyword?apiKey={self._api_key}",
            json_body={
                "SearchByKeywordRequest": {
                    "keyword": keyword,
                    "records": records,
                    "startingRecord": 0,
                }
            },
        )
        return data.get("SearchResults", {}).get("Parts", [])

    def search_by_part_number(
        self, part_number: str
    ) -> list[dict[str, Any]]:
        """Search Mouser by manufacturer part number."""
        data = self._request(
            "POST",
            f"/search/partnumber?apiKey={self._api_key}",
            json_body={
                "SearchByPartRequest": {
                    "mouserPartNumber": part_number,
                    "partSearchOptions": "Exact",
                }
            },
        )
        return data.get("SearchResults", {}).get("Parts", [])

    def parse_price(self, part: dict[str, Any]) -> CommercialPrice:
        """Convert Mouser part result to CommercialPrice."""
        price_breaks = []
        for pb in part.get("PriceBreaks", []):
            price_str = pb.get("Price", "0").replace("$", "").replace(",", "")
            try:
                price_val = float(price_str)
            except ValueError:
                price_val = 0.0
            price_breaks.append(
                {
                    "quantity": pb.get("Quantity", 0),
                    "unit_price": price_val,
                }
            )

        # Use first price break as unit price
        unit_price = price_breaks[0]["unit_price"] if price_breaks else 0.0

        avail_str = part.get("Availability", "0").split(" ")[0].replace(",", "")
        try:
            avail = int(avail_str)
        except ValueError:
            avail = 0

        return CommercialPrice(
            part_number=part.get("ManufacturerPartNumber", ""),
            manufacturer=part.get("Manufacturer", ""),
            description=part.get("Description", ""),
            unit_price=unit_price,
            currency="USD",
            source="mouser",
            url=part.get("ProductDetailUrl", ""),
            quantity_available=avail,
            price_breaks=price_breaks,
        )
