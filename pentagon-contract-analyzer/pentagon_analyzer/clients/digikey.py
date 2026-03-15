"""DigiKey API v4 client for commercial electronics pricing."""

from __future__ import annotations

import logging
import time
from typing import Any

from pentagon_analyzer.clients.base import BaseAPIClient
from pentagon_analyzer.models.pricing import CommercialPrice

logger = logging.getLogger(__name__)


class DigiKeyClient(BaseAPIClient):
    """Client for DigiKey Product Search API v4.

    Docs: https://developer.digikey.com/
    Auth: OAuth2 client credentials flow. Token valid ~30 min.
    """

    BASE_URL = "https://api.digikey.com/products/v4"
    TOKEN_URL = "https://api.digikey.com/v1/oauth2/token"

    def __init__(self, client_id: str, client_secret: str, **kwargs: Any):
        super().__init__(**kwargs)
        self._client_id = client_id
        self._client_secret = client_secret
        self._token: str | None = None
        self._token_expires: float = 0

    def _ensure_token(self) -> None:
        """Obtain/refresh OAuth2 token via client credentials flow."""
        if self._token and time.time() < self._token_expires:
            return
        resp = self._session.post(
            self.TOKEN_URL,
            data={
                "client_id": self._client_id,
                "client_secret": self._client_secret,
                "grant_type": "client_credentials",
            },
        )
        resp.raise_for_status()
        body = resp.json()
        self._token = body["access_token"]
        self._token_expires = time.time() + body.get("expires_in", 1800) - 60

    def _auth_headers(self) -> dict[str, str]:
        self._ensure_token()
        return {
            "Authorization": f"Bearer {self._token}",
            "X-DIGIKEY-Client-Id": self._client_id,
        }

    def search_products(self, keyword: str, limit: int = 10) -> list[dict[str, Any]]:
        """Search DigiKey product catalog by keyword/part number."""
        data = self._request(
            "POST",
            "/search/keyword",
            json_body={"Keywords": keyword, "RecordCount": limit},
            headers=self._auth_headers(),
        )
        return data.get("Products", [])

    def get_pricing(
        self, digikey_part_number: str, quantity: int = 1
    ) -> list[dict[str, Any]]:
        """Get quantity-break pricing for a specific part."""
        data = self._request(
            "GET",
            f"/search/{digikey_part_number}/pricing",
            params={"requestedQuantity": quantity},
            headers=self._auth_headers(),
        )
        return data.get("ProductPricing", [])

    def parse_price(self, product: dict[str, Any]) -> CommercialPrice:
        """Convert DigiKey product result to CommercialPrice."""
        price_breaks = []
        for pb in product.get("StandardPricing", []):
            price_breaks.append(
                {
                    "quantity": pb.get("BreakQuantity", 0),
                    "unit_price": pb.get("UnitPrice", 0.0),
                }
            )

        return CommercialPrice(
            part_number=product.get("ManufacturerPartNumber", ""),
            manufacturer=product.get("Manufacturer", {}).get("Name", ""),
            description=product.get("Description", {}).get("ProductDescription", ""),
            unit_price=product.get("UnitPrice", 0.0),
            currency="USD",
            source="digikey",
            url=product.get("ProductUrl", ""),
            quantity_available=product.get("QuantityAvailable", 0),
            price_breaks=price_breaks,
        )
