"""Analysis result data models."""

from __future__ import annotations

from dataclasses import dataclass, field

from pentagon_analyzer.models.contract import CLIN
from pentagon_analyzer.models.pricing import CommercialMatch
from pentagon_analyzer.models.vendor import VendorScore


@dataclass
class MarkupAnalysis:
    """Markup analysis for a single CLIN."""

    clin: CLIN
    award_id: str
    recipient_name: str
    commercial_match: CommercialMatch | None
    markup_ratio: float | None  # contract_price / commercial_price
    markup_amount: float | None  # absolute dollar difference per unit
    total_overspend: float | None  # markup_amount * quantity
    flagged: bool
    flag_tier: str  # "none", "10x", "25x", "50x", "100x"

    @property
    def flag_reason(self) -> str:
        if not self.flagged:
            return ""
        return (
            f"${self.clin.unit_price:,.2f} gov vs "
            f"${self.commercial_match.commercial_unit_price:,.2f} commercial "
            f"({self.markup_ratio:.1f}x markup)"
        )


@dataclass
class FlaggedContract:
    """Aggregated analysis for an entire contract."""

    award_id: str
    piid: str
    recipient_name: str
    recipient_uei: str
    awarding_agency: str
    total_award_amount: float
    flagged_clins: list[MarkupAnalysis]
    total_overspend: float
    max_markup_ratio: float
    vendor_score: VendorScore | None
    ease_of_undercut: float  # 0.0 - 1.0
    competitor_count: int


@dataclass
class AnalysisSummary:
    """Top-level summary of an analysis run."""

    total_contracts_analyzed: int
    total_clins_analyzed: int
    contracts_flagged: int
    clins_flagged: int
    total_potential_savings: float
    flagged_contracts: list[FlaggedContract] = field(default_factory=list)
    contracts_above_50x: int = 0
    contracts_above_100x: int = 0
    single_supplier_count: int = 0
