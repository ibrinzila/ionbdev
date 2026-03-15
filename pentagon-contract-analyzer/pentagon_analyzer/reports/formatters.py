"""Output formatting helpers for analysis results."""

from __future__ import annotations

import csv
import io
import json
from typing import Any

from pentagon_analyzer.models.analysis import AnalysisSummary, FlaggedContract, MarkupAnalysis


def to_json(summary: AnalysisSummary) -> str:
    """Serialize analysis summary to JSON."""
    return json.dumps(_summary_to_dict(summary), indent=2, default=str)


def to_csv(summary: AnalysisSummary) -> str:
    """Serialize flagged contracts to CSV."""
    output = io.StringIO()
    writer = csv.writer(output)
    writer.writerow([
        "Award ID",
        "PIID",
        "Recipient",
        "Agency",
        "CLIN",
        "Description",
        "Gov Unit Price",
        "Commercial Price",
        "Source",
        "Markup Ratio",
        "Total Overspend",
        "Confidence",
        "Ease of Undercut",
        "Competitors",
    ])

    for fc in summary.flagged_contracts:
        for ma in fc.flagged_clins:
            writer.writerow([
                fc.award_id,
                fc.piid,
                fc.recipient_name,
                fc.awarding_agency,
                ma.clin.clin_number,
                ma.clin.description[:100],
                f"{ma.clin.unit_price:.2f}",
                f"{ma.commercial_match.commercial_unit_price:.2f}" if ma.commercial_match else "",
                ma.commercial_match.source if ma.commercial_match else "",
                f"{ma.markup_ratio:.1f}" if ma.markup_ratio else "",
                f"{ma.total_overspend:.2f}" if ma.total_overspend else "",
                f"{ma.commercial_match.confidence:.2f}" if ma.commercial_match else "",
                f"{fc.ease_of_undercut:.3f}",
                str(fc.competitor_count),
            ])

    return output.getvalue()


def _summary_to_dict(summary: AnalysisSummary) -> dict[str, Any]:
    """Convert AnalysisSummary to a serializable dict."""
    return {
        "summary": {
            "total_contracts_analyzed": summary.total_contracts_analyzed,
            "total_clins_analyzed": summary.total_clins_analyzed,
            "contracts_flagged": summary.contracts_flagged,
            "clins_flagged": summary.clins_flagged,
            "total_potential_savings": summary.total_potential_savings,
            "contracts_above_50x": summary.contracts_above_50x,
            "contracts_above_100x": summary.contracts_above_100x,
            "single_supplier_count": summary.single_supplier_count,
        },
        "flagged_contracts": [
            _flagged_to_dict(fc) for fc in summary.flagged_contracts
        ],
    }


def _flagged_to_dict(fc: FlaggedContract) -> dict[str, Any]:
    """Convert FlaggedContract to dict."""
    return {
        "award_id": fc.award_id,
        "piid": fc.piid,
        "recipient_name": fc.recipient_name,
        "recipient_uei": fc.recipient_uei,
        "awarding_agency": fc.awarding_agency,
        "total_award_amount": fc.total_award_amount,
        "total_overspend": fc.total_overspend,
        "max_markup_ratio": fc.max_markup_ratio,
        "ease_of_undercut": fc.ease_of_undercut,
        "competitor_count": fc.competitor_count,
        "flagged_clins": [_markup_to_dict(ma) for ma in fc.flagged_clins],
    }


def _markup_to_dict(ma: MarkupAnalysis) -> dict[str, Any]:
    """Convert MarkupAnalysis to dict."""
    return {
        "clin_number": ma.clin.clin_number,
        "description": ma.clin.description,
        "gov_unit_price": ma.clin.unit_price,
        "quantity": ma.clin.quantity,
        "commercial_price": (
            ma.commercial_match.commercial_unit_price if ma.commercial_match else None
        ),
        "commercial_source": ma.commercial_match.source if ma.commercial_match else None,
        "commercial_part": (
            ma.commercial_match.matched_part_number if ma.commercial_match else None
        ),
        "markup_ratio": ma.markup_ratio,
        "markup_amount": ma.markup_amount,
        "total_overspend": ma.total_overspend,
        "flag_tier": ma.flag_tier,
        "confidence": ma.commercial_match.confidence if ma.commercial_match else None,
    }
