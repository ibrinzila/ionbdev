"""Tests for SAM.gov Contract Awards API client."""

from __future__ import annotations

from unittest.mock import MagicMock

from pentagon_analyzer.clients.sam_contracts import SAMContractsClient


def test_search_builds_params(tmp_cache):
    client = SAMContractsClient("test_key", cache=tmp_cache)
    client._request = MagicMock(return_value={"awardSummary": [], "totalRecords": 0})

    list(client.search_awards(
        naics_codes=["334417", "541512"],
        min_dollars=5000,
        department_code="9700",
    ))

    call_args = client._request.call_args
    params = call_args.kwargs.get("params") or call_args[1].get("params")
    assert params["naicsCode"] == "334417~541512"
    assert "[5000" in params["dollarsObligated"]
    assert params["contractingDepartmentCode"] == "9700"
    assert params["api_key"] == "test_key"


def test_get_contract_by_piid(tmp_cache):
    client = SAMContractsClient("test_key", cache=tmp_cache)
    client._request = MagicMock(return_value={
        "awardSummary": [{"contractId": {"piid": "W52P1J"}}]
    })

    result = client.get_contract_by_piid("W52P1J")
    assert result is not None
    assert result["contractId"]["piid"] == "W52P1J"


def test_get_contract_not_found(tmp_cache):
    client = SAMContractsClient("test_key", cache=tmp_cache)
    client._request = MagicMock(return_value={"awardSummary": []})

    result = client.get_contract_by_piid("NONEXISTENT")
    assert result is None
