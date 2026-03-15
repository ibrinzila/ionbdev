"""USAspending.gov v2 API client for federal contract award data."""

from __future__ import annotations

import logging
from datetime import date
from typing import Any, Iterator

from pentagon_analyzer.clients.base import BaseAPIClient
from pentagon_analyzer.models.contract import Award

logger = logging.getLogger(__name__)


class USASpendingClient(BaseAPIClient):
    """Client for USAspending.gov v2 API.

    Docs: https://api.usaspending.gov/
    No auth required. Rate limited (HTTP 429).
    """

    BASE_URL = "https://api.usaspending.gov/api/v2"

    AWARD_FIELDS = [
        "Award ID",
        "Recipient Name",
        "Recipient UEI",
        "Award Amount",
        "Start Date",
        "End Date",
        "Awarding Agency",
        "Awarding Sub Agency",
        "Description",
        "NAICS Code",
        "PSC Code",
        "Contract Award Type",
        "generated_internal_id",
    ]

    def search_contract_awards(
        self,
        *,
        keywords: list[str] | None = None,
        naics_codes: list[str] | None = None,
        psc_codes: list[str] | None = None,
        agency_name: str | None = None,
        time_period: list[dict[str, str]] | None = None,
        min_amount: float | None = None,
        max_amount: float | None = None,
        page_size: int = 100,
    ) -> Iterator[dict[str, Any]]:
        """Paginate through contract awards. Yields raw award dicts.

        Only returns contracts (award_type_codes A-D).
        """
        filters: dict[str, Any] = {"award_type_codes": ["A", "B", "C", "D"]}

        if keywords:
            filters["keywords"] = keywords
        if naics_codes:
            filters["naics_codes"] = {"require": naics_codes}
        if psc_codes:
            filters["psc_codes"] = {"require": psc_codes}
        if time_period:
            filters["time_period"] = time_period
        if min_amount is not None or max_amount is not None:
            bounds: dict[str, float] = {}
            if min_amount is not None:
                bounds["lower_bound"] = min_amount
            if max_amount is not None:
                bounds["upper_bound"] = max_amount
            filters["award_amounts"] = [bounds]
        if agency_name:
            filters["agencies"] = [
                {"type": "awarding", "tier": "toptier", "name": agency_name}
            ]

        page = 1
        while True:
            data = self._request(
                "POST",
                "/search/spending_by_award/",
                json_body={
                    "filters": filters,
                    "fields": self.AWARD_FIELDS,
                    "limit": page_size,
                    "page": page,
                    "sort": "Award Amount",
                    "order": "desc",
                },
            )
            results = data.get("results", [])
            yield from results

            has_next = data.get("page_metadata", {}).get("hasNext", False)
            if not has_next or not results:
                break
            page += 1

    def get_award_detail(self, generated_internal_id: str) -> dict[str, Any]:
        """Fetch detailed award info including transactions."""
        return self._request("GET", f"/awards/{generated_internal_id}/")

    def parse_award(self, raw: dict[str, Any]) -> Award:
        """Convert raw USAspending result to Award dataclass."""
        start = _parse_date(raw.get("Start Date"))
        end = _parse_date(raw.get("End Date"))
        return Award(
            award_id=raw.get("Award ID", ""),
            internal_id=raw.get("generated_internal_id", ""),
            piid=raw.get("Award ID", ""),
            recipient_name=raw.get("Recipient Name", ""),
            recipient_uei=raw.get("Recipient UEI", ""),
            award_amount=float(raw.get("Award Amount", 0) or 0),
            start_date=start,
            end_date=end,
            awarding_agency=raw.get("Awarding Agency", ""),
            awarding_sub_agency=raw.get("Awarding Sub Agency", ""),
            description=raw.get("Description", ""),
            naics_code=raw.get("NAICS Code", ""),
            psc_code=raw.get("PSC Code", ""),
            award_type=raw.get("Contract Award Type", ""),
        )


def _parse_date(val: str | None) -> date | None:
    if not val:
        return None
    try:
        return date.fromisoformat(val)
    except ValueError:
        return None
