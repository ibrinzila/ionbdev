"""Tests for XGBoost undercut ranking model."""

import numpy as np
import pandas as pd
import pytest

from pentagon_analyzer.scoring.model import UndercutRanker


@pytest.fixture
def sample_features():
    return pd.DataFrame([
        {
            "markup_ratio": 86.5,
            "competition_score": 0.15,
            "contract_value_log": 11.76,
            "vendor_past_awards": 47,
            "is_small_business": 0,
            "competitors_in_naics": 15,
            "match_confidence": 0.72,
            "total_overspend": 126520.0,
        },
        {
            "markup_ratio": 11.8,
            "competition_score": 0.85,
            "contract_value_log": 8.5,
            "vendor_past_awards": 3,
            "is_small_business": 1,
            "competitors_in_naics": 85,
            "match_confidence": 0.91,
            "total_overspend": 5000.0,
        },
    ])


def test_heuristic_fallback(sample_features):
    """Without a trained model, should use heuristic scoring."""
    ranker = UndercutRanker()
    assert not ranker.has_model

    scores = ranker.predict(sample_features)
    assert len(scores) == 2
    assert all(0 <= s <= 1 for s in scores)

    # First item has 86.5x markup (high weight on markup_ratio=0.4),
    # second has only 11.8x but higher competition/confidence.
    # Both should produce reasonable scores.
    assert scores[0] > 0.3
    assert scores[1] > 0.3


def test_heuristic_score_bounds():
    """Heuristic score should always be in [0, 1]."""
    ranker = UndercutRanker()

    extreme_row = pd.Series({
        "markup_ratio": 1000,
        "competition_score": 1.0,
        "contract_value_log": 0,
        "vendor_past_awards": 0,
        "is_small_business": 1,
        "competitors_in_naics": 100,
        "match_confidence": 1.0,
        "total_overspend": 999999,
    })
    score = ranker._heuristic_score(extreme_row)
    assert 0 <= score <= 1

    minimal_row = pd.Series({
        "markup_ratio": 0,
        "competition_score": 0,
        "contract_value_log": 25,
        "vendor_past_awards": 100,
        "is_small_business": 0,
        "competitors_in_naics": 0,
        "match_confidence": 0,
        "total_overspend": 0,
    })
    score = ranker._heuristic_score(minimal_row)
    assert 0 <= score <= 1
