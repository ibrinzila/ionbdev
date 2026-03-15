"""Vendor competition scoring from SAM.gov entity data."""

from __future__ import annotations

import logging
from typing import Any

from pentagon_analyzer.clients.sam_entities import SAMEntitiesClient
from pentagon_analyzer.models.vendor import VendorProfile, VendorScore

logger = logging.getLogger(__name__)


class VendorScorer:
    """Score vendors on competition level and ease of undercut."""

    def __init__(self, sam_entities: SAMEntitiesClient):
        self._sam = sam_entities
        self._naics_cache: dict[str, int] = {}

    def score_vendor(
        self,
        uei: str,
        naics_code: str,
        total_obligated: float = 0,
        past_award_count: int = 0,
    ) -> VendorScore:
        """Score a vendor's competitive position.

        Factors:
        - Number of active competitors in same NAICS code
        - Business size category
        - Past award history
        - Registration status
        """
        entity = self._sam.get_entity_by_uei(uei)
        if not entity:
            return _default_score(uei, naics_code)

        profile = self._sam.parse_entity(entity)
        competitors = self._count_competitors(naics_code)

        comp_score = _normalize_competition(competitors)
        size_cat = _size_category(profile)
        risk = _composite_risk(comp_score, size_cat, past_award_count)

        return VendorScore(
            uei=profile.uei,
            legal_name=profile.legal_name,
            cage_code=profile.cage_code,
            competition_score=comp_score,
            size_category=size_cat,
            past_award_count=past_award_count,
            total_obligated=total_obligated,
            registration_status=profile.registration_status,
            competitors_in_naics=competitors,
            overall_risk_score=risk,
        )

    def _count_competitors(self, naics_code: str) -> int:
        """Count active entities for a NAICS code (cached)."""
        if naics_code in self._naics_cache:
            return self._naics_cache[naics_code]
        try:
            count = self._sam.count_competitors(naics_code)
        except Exception as exc:
            logger.warning("Failed to count competitors for NAICS %s: %s", naics_code, exc)
            count = 0
        self._naics_cache[naics_code] = count
        return count


def _normalize_competition(count: int) -> float:
    """Normalize competitor count to [0, 1]. More competitors = higher score."""
    if count <= 1:
        return 0.0
    if count >= 100:
        return 1.0
    return min(count / 100.0, 1.0)


def _size_category(profile: VendorProfile) -> str:
    """Determine vendor size category."""
    if profile.small_business:
        return "small"
    return "large"


def _composite_risk(
    competition_score: float, size_category: str, past_awards: int
) -> float:
    """Composite risk score [0, 1]. Higher = easier to undercut.

    Weights:
    - Competition (0.5): more competitors = easier to undercut
    - Size (0.2): large vendors harder to undercut
    - Award history (0.3): fewer past awards = easier to undercut
    """
    size_factor = 0.7 if size_category == "large" else 1.0
    award_factor = max(0, 1.0 - (past_awards / 50.0))

    return (
        competition_score * 0.5
        + size_factor * 0.2
        + award_factor * 0.3
    )


def _default_score(uei: str, naics_code: str) -> VendorScore:
    """Default score when vendor entity can't be found."""
    return VendorScore(
        uei=uei,
        legal_name="Unknown",
        cage_code="",
        competition_score=0.5,
        size_category="unknown",
        past_award_count=0,
        total_obligated=0,
        registration_status="Unknown",
        competitors_in_naics=0,
        overall_risk_score=0.5,
    )
