"""Tests for price comparison and markup analysis."""

from pentagon_analyzer.matching.price_comparator import analyze_markup, batch_analyze, _compute_tier
from pentagon_analyzer.models.contract import CLIN
from pentagon_analyzer.models.pricing import CommercialMatch


def test_analyze_markup_flagged():
    clin = CLIN("0001", "Connector plug", 100, 1280.00, 128000.00, "EA", "5935")
    match = CommercialMatch(
        "0001", "Connector plug", "D38999", "Amphenol",
        14.80, "digikey", 0.72, "connector plug", "",
    )
    result = analyze_markup(clin, "AWARD-1", "ACME Corp", match, threshold=10.0)

    assert result.flagged is True
    assert result.markup_ratio == pytest.approx(86.49, rel=0.01)
    assert result.markup_amount == pytest.approx(1265.20, rel=0.01)
    assert result.total_overspend == pytest.approx(126520.00, rel=0.01)
    assert result.flag_tier == "50x"


def test_analyze_markup_not_flagged():
    clin = CLIN("0001", "Standard bolt", 1000, 5.00, 5000.00, "EA")
    match = CommercialMatch("0001", "Bolt", "B123", "Mfr", 2.50, "mouser", 0.8, "bolt", "")

    result = analyze_markup(clin, "AWARD-2", "Corp", match, threshold=10.0)
    assert result.flagged is False
    assert result.markup_ratio == pytest.approx(2.0)
    assert result.flag_tier == "none"


def test_analyze_markup_no_match():
    clin = CLIN("0001", "Custom assembly", 1, 50000, 50000)
    result = analyze_markup(clin, "AWARD-3", "Corp", None, threshold=10.0)

    assert result.flagged is False
    assert result.markup_ratio is None


def test_compute_tier():
    assert _compute_tier(150) == "100x"
    assert _compute_tier(75) == "50x"
    assert _compute_tier(30) == "25x"
    assert _compute_tier(15) == "10x"
    assert _compute_tier(5) == "none"


def test_batch_analyze_sorted():
    clins = [
        (CLIN("0001", "Item A", 1, 500, 500, "EA"), "A1", "Corp1",
         CommercialMatch("0001", "A", "P1", "M", 5.0, "dk", 0.8, "a", "")),
        (CLIN("0002", "Item B", 1, 100, 100, "EA"), "A2", "Corp2",
         CommercialMatch("0002", "B", "P2", "M", 50.0, "dk", 0.8, "b", "")),
    ]
    results = batch_analyze(clins, threshold=10.0)

    # Item A (100x) should be first, Item B (2x) not flagged
    assert results[0].flagged is True
    assert results[0].markup_ratio == pytest.approx(100.0)
    assert results[1].flagged is False


import pytest
