"""SAM.gov Contract Awards API v1 client (replaces FPDS, decommissioned Feb 2026)."""

from __future__ import annotations

import logging
from typing import Any, Iterator

from pentagon_analyzer.clients.base import BaseAPIClient

logger = logging.getLogger(__name__)


class SAMContractsClient(BaseAPIClient):
    """Client for SAM.gov Contract Awards API v1.

    Docs: https://open.gsa.gov/api/contract-awards/
    Requires SAM.gov API key. Rate limits: 10-10,000 req/day.
    """

    BASE_URL = "https://api.sam.gov/contract-awards/v1"

    def __init__(self, api_key: str, **kwargs: Any):
        super().__init__(**kwargs)
        self._api_key = api_key

    def search_awards(
        self,
        *,
        naics_codes: list[str] | None = None,
        psc_codes: list[str] | None = None,
        min_dollars: float | None = None,
        max_dollars: float | None = None,
        department_code: str | None = None,
        last_modified_since: str | None = None,
        limit: int = 100,
    ) -> Iterator[dict[str, Any]]:
        """Paginate through SAM contract award search results.

        Args:
            naics_codes: NAICS codes (tilde-separated in API).
            psc_codes: Product/Service codes.
            min_dollars: Minimum obligated dollars.
            max_dollars: Maximum obligated dollars.
            department_code: Contracting department code (e.g. "9700" for DOD).
            last_modified_since: Date string MM/DD/YYYY.
            limit: Page size (max 100).
        """
        params: dict[str, Any] = {"api_key": self._api_key, "limit": limit}

        if naics_codes:
            params["naicsCode"] = "~".join(naics_codes)
        if psc_codes:
            params["productOrServiceCode"] = "~".join(psc_codes)
        if min_dollars is not None or max_dollars is not None:
            lo = min_dollars or 0.0
            hi = max_dollars or 999999999999.99
            params["dollarsObligated"] = f"[{lo},{hi}]"
        if department_code:
            params["contractingDepartmentCode"] = department_code
        if last_modified_since:
            params["lastModifiedDate"] = f"[{last_modified_since},]"

        params["includeSections"] = "contractId,coreData,awardDetails,awardeeData"

        offset = 0
        while True:
            params["offset"] = offset
            data = self._request("GET", "/search", params=params)
            records = data.get("awardSummary", [])
            yield from records

            total = data.get("totalRecords", 0)
            offset += limit
            if offset >= total or not records:
                break

    def get_contract_by_piid(self, piid: str) -> dict[str, Any] | None:
        """Fetch a specific contract by its Procurement Instrument Identifier."""
        params = {
            "api_key": self._api_key,
            "piid": piid,
            "includeSections": "contractId,coreData,awardDetails,awardeeData",
        }
        data = self._request("GET", "/search", params=params)
        records = data.get("awardSummary", [])
        return records[0] if records else None
