"""Tests for CLIN parser and COTS detection."""

from pentagon_analyzer.matching.clin_parser import (
    clean_description,
    is_cots_candidate,
    parse_clins_from_usaspending,
)
from pentagon_analyzer.models.contract import CLIN


def test_is_cots_candidate_supply_psc():
    clin = CLIN(
        clin_number="0001",
        description="Generic item",
        quantity=1,
        unit_price=100,
        total_cost=100,
        psc_code="5935",  # Starts with digit = supply
    )
    assert is_cots_candidate(clin) is True


def test_is_cots_candidate_service_psc():
    clin = CLIN(
        clin_number="0001",
        description="Professional consulting services",
        quantity=1,
        unit_price=100000,
        total_cost=100000,
        psc_code="R408",  # Starts with letter = service
    )
    assert is_cots_candidate(clin) is False


def test_is_cots_candidate_keyword():
    clin = CLIN(
        clin_number="0001",
        description="CIRCUIT BREAKER, 30 AMP, SINGLE POLE",
        quantity=50,
        unit_price=3400,
        total_cost=170000,
        unit_of_measure="EA",
        psc_code="",
    )
    assert is_cots_candidate(clin) is True


def test_is_cots_candidate_connector():
    clin = CLIN(
        clin_number="0001",
        description="Ruggedized connector assembly per MIL-DTL-38999",
        quantity=10,
        unit_price=1280,
        total_cost=12800,
        psc_code="R999",  # service code, but "connector" keyword
    )
    assert is_cots_candidate(clin) is True


def test_clean_description_removes_nsn():
    assert "NSN" not in clean_description("CONNECTOR NSN 5935-01-234-5678 PLUG TYPE")


def test_clean_description_removes_mil_spec():
    cleaned = clean_description("BREAKER MIL-DTL-38999 30 AMP")
    assert "MIL-DTL-38999" not in cleaned
    assert "BREAKER" in cleaned


def test_clean_description_removes_cage():
    cleaned = clean_description("ITEM CAGE ABC12 ASSEMBLY")
    assert "CAGE ABC12" not in cleaned


def test_parse_clins_from_usaspending():
    data = {
        "Description": "CONNECTOR PLUG ELECTRICAL",
        "Award Amount": 50000,
        "PSC Code": "5935",
        "NAICS Code": "334417",
    }
    clins = parse_clins_from_usaspending(data)
    assert len(clins) == 1
    assert clins[0].description == "CONNECTOR PLUG ELECTRICAL"
    assert clins[0].unit_price == 50000
