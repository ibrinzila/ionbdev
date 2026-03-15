"""Feature engineering for XGBoost undercut ranking model."""

from __future__ import annotations

import math

import pandas as pd

from pentagon_analyzer.models.analysis import MarkupAnalysis
from pentagon_analyzer.models.vendor import VendorScore


def build_feature_matrix(
    analyses: list[MarkupAnalysis],
    vendor_scores: dict[str, VendorScore],
) -> pd.DataFrame:
    """Build feature DataFrame for XGBoost model.

    Features per contract/CLIN:
    - markup_ratio: float
    - competition_score: float (from vendor scorer)
    - contract_value_log: float (log-transformed award amount)
    - vendor_past_awards: int
    - is_small_business: bool -> int
    - competitors_in_naics: int
    - match_confidence: float (from product matcher)
    - total_overspend: float
    """
    rows = []
    for analysis in analyses:
        if not analysis.flagged or analysis.markup_ratio is None:
            continue

        uei = ""  # Would come from award data
        vs = vendor_scores.get(uei)

        row = {
            "award_id": analysis.award_id,
            "clin_number": analysis.clin.clin_number,
            "markup_ratio": analysis.markup_ratio,
            "competition_score": vs.competition_score if vs else 0.5,
            "contract_value_log": math.log1p(analysis.clin.total_cost),
            "vendor_past_awards": vs.past_award_count if vs else 0,
            "is_small_business": int(vs.size_category == "small") if vs else 0,
            "competitors_in_naics": vs.competitors_in_naics if vs else 0,
            "match_confidence": (
                analysis.commercial_match.confidence
                if analysis.commercial_match
                else 0
            ),
            "total_overspend": analysis.total_overspend or 0,
        }
        rows.append(row)

    return pd.DataFrame(rows)


FEATURE_COLUMNS = [
    "markup_ratio",
    "competition_score",
    "contract_value_log",
    "vendor_past_awards",
    "is_small_business",
    "competitors_in_naics",
    "match_confidence",
    "total_overspend",
]
