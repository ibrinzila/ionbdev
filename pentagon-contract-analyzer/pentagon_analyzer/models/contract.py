"""Contract and CLIN data models."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date


@dataclass
class CLIN:
    """Contract Line Item Number."""

    clin_number: str  # e.g. "0001" or "0001AA"
    description: str
    quantity: int
    unit_price: float
    total_cost: float
    unit_of_measure: str = ""
    psc_code: str = ""
    naics_code: str = ""

    @property
    def is_sub_clin(self) -> bool:
        return len(self.clin_number) > 4


@dataclass
class Award:
    """A federal contract award."""

    award_id: str
    internal_id: str
    piid: str  # Procurement Instrument Identifier
    recipient_name: str
    recipient_uei: str
    award_amount: float
    start_date: date | None
    end_date: date | None
    awarding_agency: str
    awarding_sub_agency: str
    description: str
    naics_code: str
    psc_code: str
    award_type: str
    clins: list[CLIN] = field(default_factory=list)

    @property
    def has_clins(self) -> bool:
        return len(self.clins) > 0
