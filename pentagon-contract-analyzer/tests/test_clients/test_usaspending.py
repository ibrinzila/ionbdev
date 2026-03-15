"""Tests for USAspending API client."""

from __future__ import annotations

from unittest.mock import MagicMock, patch
from datetime import date

from pentagon_analyzer.clients.usaspending import USASpendingClient, _parse_date


def test_parse_date_valid():
    assert _parse_date("2024-01-15") == date(2024, 1, 15)


def test_parse_date_none():
    assert _parse_date(None) is None


def test_parse_date_invalid():
    assert _parse_date("not-a-date") is None


def test_parse_award(tmp_cache):
    client = USASpendingClient(cache=tmp_cache)
    raw = {
        "Award ID": "W52P1J-20-C-0042",
        "generated_internal_id": "gen_123",
        "Recipient Name": "ACME Corp",
        "Recipient UEI": "ABC123",
        "Award Amount": 50000.00,
        "Start Date": "2024-01-01",
        "End Date": "2025-01-01",
        "Awarding Agency": "Department of Defense",
        "Awarding Sub Agency": "Army",
        "Description": "Connector plugs",
        "NAICS Code": "334417",
        "PSC Code": "5935",
        "Contract Award Type": "Contract",
    }
    award = client.parse_award(raw)
    assert award.award_id == "W52P1J-20-C-0042"
    assert award.recipient_name == "ACME Corp"
    assert award.award_amount == 50000.00
    assert award.start_date == date(2024, 1, 1)
    assert award.naics_code == "334417"


def test_search_builds_correct_filters(tmp_cache):
    client = USASpendingClient(cache=tmp_cache)
    client._request = MagicMock(return_value={"results": [], "page_metadata": {"hasNext": False}})

    list(client.search_contract_awards(
        agency_name="Department of Defense",
        naics_codes=["334417"],
        min_amount=10000,
    ))

    call_args = client._request.call_args
    body = call_args.kwargs["json_body"]
    assert body["filters"]["award_type_codes"] == ["A", "B", "C", "D"]
    assert body["filters"]["naics_codes"] == {"require": ["334417"]}
    assert body["filters"]["award_amounts"] == [{"lower_bound": 10000}]
    assert body["filters"]["agencies"][0]["name"] == "Department of Defense"
