"""SAM.gov Entity Management API v3 client for vendor profiles."""

from __future__ import annotations

import logging
from typing import Any

from pentagon_analyzer.clients.base import BaseAPIClient
from pentagon_analyzer.models.vendor import VendorProfile

logger = logging.getLogger(__name__)


class SAMEntitiesClient(BaseAPIClient):
    """Client for SAM.gov Entity Management API v3.

    Docs: https://open.gsa.gov/api/entity-api/
    Requires SAM.gov API key. Rate limits: 10-10,000 req/day.
    """

    BASE_URL = "https://api.sam.gov/entity-information/v3"

    def __init__(self, api_key: str, **kwargs: Any):
        super().__init__(**kwargs)
        self._api_key = api_key

    def get_entity_by_uei(self, uei: str) -> dict[str, Any] | None:
        """Look up a single entity by UEI SAM identifier."""
        params = {
            "api_key": self._api_key,
            "ueiSAM": uei,
            "registrationStatus": "A",
            "includeSections": "entityRegistration,coreData,assertions",
        }
        data = self._request("GET", "/entities", params=params)
        entities = data.get("entityData", [])
        return entities[0] if entities else None

    def get_entity_by_cage(self, cage_code: str) -> dict[str, Any] | None:
        """Look up a single entity by CAGE code."""
        params = {
            "api_key": self._api_key,
            "cageCode": cage_code,
            "registrationStatus": "A",
            "includeSections": "entityRegistration,coreData,assertions",
        }
        data = self._request("GET", "/entities", params=params)
        entities = data.get("entityData", [])
        return entities[0] if entities else None

    def search_entities_by_naics(
        self, naics_code: str, *, active_only: bool = True
    ) -> list[dict[str, Any]]:
        """Find all SAM-registered entities for a NAICS code.

        Used to assess competition level for vendor scoring.
        """
        params: dict[str, Any] = {
            "api_key": self._api_key,
            "naicsCode": naics_code,
            "includeSections": "entityRegistration,coreData,assertions",
        }
        if active_only:
            params["registrationStatus"] = "A"
        data = self._request("GET", "/entities", params=params)
        return data.get("entityData", [])

    def count_competitors(self, naics_code: str) -> int:
        """Count active entities registered for a given NAICS code."""
        entities = self.search_entities_by_naics(naics_code)
        return len(entities)

    def parse_entity(self, raw: dict[str, Any]) -> VendorProfile:
        """Convert raw SAM entity data to VendorProfile."""
        reg = raw.get("entityRegistration", {})
        core = raw.get("coreData", {})
        phys = core.get("physicalAddress", {})
        assertions = raw.get("assertions", {})

        naics_list = []
        for n in assertions.get("naicsCodeList", []):
            if isinstance(n, dict):
                naics_list.append(n.get("naicsCode", ""))
            else:
                naics_list.append(str(n))

        return VendorProfile(
            uei=reg.get("ueiSAM", ""),
            cage_code=reg.get("cageCode", ""),
            legal_name=reg.get("legalBusinessName", ""),
            dba_name=reg.get("dbaName", ""),
            physical_city=phys.get("city", ""),
            physical_state=phys.get("stateOrProvinceCode", ""),
            business_types=reg.get("businessTypes", []),
            naics_codes=naics_list,
            psc_codes=assertions.get("pscCodeList", []),
            registration_status=reg.get("registrationStatus", ""),
            entity_type=reg.get("entityType", ""),
            small_business="2X" in reg.get("businessTypes", []),
            registration_date=reg.get("registrationDate", ""),
            expiration_date=reg.get("expirationDate", ""),
        )
