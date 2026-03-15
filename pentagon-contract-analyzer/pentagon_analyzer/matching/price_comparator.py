"""Compare contract pricing with commercial equivalents and flag overpriced items."""

from __future__ import annotations

from pentagon_analyzer.models.analysis import MarkupAnalysis
from pentagon_analyzer.models.contract import CLIN
from pentagon_analyzer.models.pricing import CommercialMatch


def analyze_markup(
    clin: CLIN,
    award_id: str,
    recipient_name: str,
    commercial_match: CommercialMatch | None,
    threshold: float = 10.0,
) -> MarkupAnalysis:
    """Compute markup ratio for a single CLIN and flag if above threshold."""
    if commercial_match is None or commercial_match.commercial_unit_price <= 0:
        return MarkupAnalysis(
            clin=clin,
            award_id=award_id,
            recipient_name=recipient_name,
            commercial_match=None,
            markup_ratio=None,
            markup_amount=None,
            total_overspend=None,
            flagged=False,
            flag_tier="none",
        )

    ratio = clin.unit_price / commercial_match.commercial_unit_price
    amount = clin.unit_price - commercial_match.commercial_unit_price
    overspend = amount * clin.quantity

    flagged = ratio >= threshold
    tier = _compute_tier(ratio)

    return MarkupAnalysis(
        clin=clin,
        award_id=award_id,
        recipient_name=recipient_name,
        commercial_match=commercial_match,
        markup_ratio=ratio,
        markup_amount=amount,
        total_overspend=overspend,
        flagged=flagged,
        flag_tier=tier,
    )


def batch_analyze(
    items: list[tuple[CLIN, str, str, CommercialMatch | None]],
    threshold: float = 10.0,
) -> list[MarkupAnalysis]:
    """Analyze markup for a batch of (CLIN, award_id, recipient, match) tuples.

    Returns results sorted by markup_ratio descending (highest first).
    """
    results = [
        analyze_markup(clin, award_id, recipient, match, threshold)
        for clin, award_id, recipient, match in items
    ]
    # Sort: flagged first, then by markup ratio descending
    results.sort(
        key=lambda r: (not r.flagged, -(r.markup_ratio or 0)),
    )
    return results


def _compute_tier(ratio: float) -> str:
    """Classify markup ratio into tier buckets."""
    if ratio >= 100:
        return "100x"
    if ratio >= 50:
        return "50x"
    if ratio >= 25:
        return "25x"
    if ratio >= 10:
        return "10x"
    return "none"
