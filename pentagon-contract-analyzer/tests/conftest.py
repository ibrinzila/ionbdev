"""Shared test fixtures."""

from __future__ import annotations

import pytest
from pathlib import Path
from pentagon_analyzer.cache.store import CacheStore
from pentagon_analyzer.models.contract import Award, CLIN
from pentagon_analyzer.models.pricing import CommercialMatch, CommercialPrice
from pentagon_analyzer.models.vendor import VendorScore
from pentagon_analyzer.models.analysis import MarkupAnalysis, FlaggedContract
from datetime import date


@pytest.fixture
def tmp_cache(tmp_path: Path) -> CacheStore:
    """Create a temporary cache store."""
    return CacheStore(tmp_path / "test_cache.db", ttl_hours=1)


@pytest.fixture
def sample_clin() -> CLIN:
    return CLIN(
        clin_number="0001",
        description="CONNECTOR PLUG, ELECTRICAL, MIL-DTL-38999",
        quantity=100,
        unit_price=1280.00,
        total_cost=128000.00,
        unit_of_measure="EA",
        psc_code="5935",
        naics_code="334417",
    )


@pytest.fixture
def sample_award() -> Award:
    return Award(
        award_id="W52P1J-20-C-0042",
        internal_id="gen_12345",
        piid="W52P1J-20-C-0042",
        recipient_name="ACME Defense Corp",
        recipient_uei="ZQGGHJH74DW7",
        award_amount=128000.00,
        start_date=date(2024, 1, 15),
        end_date=date(2025, 1, 14),
        awarding_agency="Department of Defense",
        awarding_sub_agency="Department of the Army",
        description="CONNECTOR PLUG, ELECTRICAL, MIL-DTL-38999",
        naics_code="334417",
        psc_code="5935",
        award_type="Contract",
    )


@pytest.fixture
def sample_commercial_match() -> CommercialMatch:
    return CommercialMatch(
        clin_number="0001",
        clin_description="CONNECTOR PLUG, ELECTRICAL",
        matched_part_number="D38999/26WB35SN",
        manufacturer="Amphenol",
        commercial_unit_price=14.80,
        source="digikey",
        confidence=0.72,
        search_query="connector plug electrical",
        url="https://www.digikey.com/en/products/detail/...",
    )


@pytest.fixture
def sample_vendor_score() -> VendorScore:
    return VendorScore(
        uei="ZQGGHJH74DW7",
        legal_name="ACME Defense Corp",
        cage_code="1ABC2",
        competition_score=0.15,
        size_category="large",
        past_award_count=47,
        total_obligated=12500000.00,
        registration_status="Active",
        competitors_in_naics=15,
        overall_risk_score=0.35,
    )


@pytest.fixture
def sample_markup_analysis(sample_clin, sample_commercial_match) -> MarkupAnalysis:
    return MarkupAnalysis(
        clin=sample_clin,
        award_id="W52P1J-20-C-0042",
        recipient_name="ACME Defense Corp",
        commercial_match=sample_commercial_match,
        markup_ratio=86.5,
        markup_amount=1265.20,
        total_overspend=126520.00,
        flagged=True,
        flag_tier="50x",
    )


@pytest.fixture
def sample_flagged_contract(sample_markup_analysis, sample_vendor_score) -> FlaggedContract:
    return FlaggedContract(
        award_id="W52P1J-20-C-0042",
        piid="W52P1J-20-C-0042",
        recipient_name="ACME Defense Corp",
        recipient_uei="ZQGGHJH74DW7",
        awarding_agency="Department of Defense",
        total_award_amount=128000.00,
        flagged_clins=[sample_markup_analysis],
        total_overspend=126520.00,
        max_markup_ratio=86.5,
        vendor_score=sample_vendor_score,
        ease_of_undercut=0.72,
        competitor_count=15,
    )
