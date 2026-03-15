"""Vendor profile and scoring data models."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class VendorProfile:
    """Vendor entity from SAM.gov."""

    uei: str
    cage_code: str
    legal_name: str
    dba_name: str
    physical_city: str
    physical_state: str
    business_types: list[str]
    naics_codes: list[str]
    psc_codes: list[str]
    registration_status: str  # "Active", "Expired"
    entity_type: str  # "Business", "Individual"
    small_business: bool
    registration_date: str
    expiration_date: str


@dataclass
class VendorScore:
    """Scoring result for a vendor's competitive position."""

    uei: str
    legal_name: str
    cage_code: str
    competition_score: float  # 0-1, higher = more competitors in NAICS
    size_category: str  # "small", "other_small", "large"
    past_award_count: int
    total_obligated: float
    registration_status: str
    competitors_in_naics: int
    overall_risk_score: float  # composite 0-1, higher = easier to undercut
