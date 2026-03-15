"""Parse CLINs from contract data and detect COTS candidates."""

from __future__ import annotations

import re
from typing import Any

from pentagon_analyzer.models.contract import CLIN

# PSC codes starting with these prefixes are supplies (not services)
SUPPLY_PSC_PREFIXES = tuple(str(i) for i in range(10))  # 0-9 = supplies
# Service PSC codes start with letters (A-Z)

# Keywords suggesting a COTS (commercial off-the-shelf) item
COTS_KEYWORDS = {
    "connector",
    "cable",
    "adapter",
    "plug",
    "switch",
    "breaker",
    "circuit",
    "resistor",
    "capacitor",
    "transformer",
    "relay",
    "fuse",
    "terminal",
    "assembly",
    "board",
    "module",
    "sensor",
    "valve",
    "pump",
    "motor",
    "battery",
    "charger",
    "tablet",
    "laptop",
    "computer",
    "display",
    "monitor",
    "printer",
    "router",
    "server",
    "drive",
    "memory",
    "power supply",
    "antenna",
    "transceiver",
    "filter",
    "amplifier",
    "oscillator",
    "diode",
    "transistor",
    "IC",
    "LED",
    "lamp",
    "bulb",
    "fastener",
    "bolt",
    "screw",
    "nut",
    "washer",
    "bearing",
    "seal",
    "gasket",
    "hose",
    "tubing",
    "wire",
    "harness",
}

# Government jargon to strip when building search queries
GOV_JARGON_PATTERNS = [
    r"\bNSN\s*[\d-]+",  # National Stock Numbers
    r"\bCAGE\s*\w+",  # CAGE codes
    r"\bMIL-\w+",  # MIL-SPEC references
    r"\bDFARS\b",
    r"\bFAR\s+\d+",
    r"\bIAW\b",
    r"\bPER\s+SPEC\b",
    r"\bSOW\b",
    r"\bPWS\b",
    r"\bCDRL\b",
    r"\bCLIN\s*\d+",
    r"\bP/N\b",
    r"\bPN:\s*",
    r"\bMFR:\s*",
    r"\bGFE\b",
    r"\bGFP\b",
    r"\bGFM\b",
    r"\bLOT\s*\d+",
    r"\bMOD\s+\d+",
    r"\(?\bEACH\b\)?",
    r"\bW/\b",
]


def parse_clins_from_usaspending(award_data: dict[str, Any]) -> list[CLIN]:
    """Extract CLINs from a USAspending award detail response.

    USAspending doesn't provide CLIN-level detail directly,
    so we create a single synthetic CLIN from the award-level data.
    """
    desc = award_data.get("description", "") or award_data.get("Description", "")
    amount = float(award_data.get("total_obligation", 0) or award_data.get("Award Amount", 0) or 0)

    return [
        CLIN(
            clin_number="0001",
            description=desc,
            quantity=1,
            unit_price=amount,
            total_cost=amount,
            unit_of_measure="LO",
            psc_code=award_data.get("psc_hierarchy", {}).get("psc_code", "")
            or award_data.get("PSC Code", ""),
            naics_code=award_data.get("naics_code", "")
            or award_data.get("NAICS Code", ""),
        )
    ]


def parse_clins_from_sam(contract_data: dict[str, Any]) -> list[CLIN]:
    """Extract CLINs from a SAM.gov contract awards response.

    SAM contract data includes coreData with line item details.
    """
    clins = []
    core_data = contract_data.get("coreData", {})
    award_details = contract_data.get("awardDetails", {})

    # SAM provides aggregated data; create CLIN from contract-level info
    description = core_data.get("descriptionOfContractRequirement", "")
    dollars = float(award_details.get("dollarsObligated", 0) or 0)
    psc = core_data.get("productOrServiceCode", "")
    naics = core_data.get("naicsCode", "")

    if description or dollars > 0:
        clins.append(
            CLIN(
                clin_number="0001",
                description=description,
                quantity=1,
                unit_price=dollars,
                total_cost=dollars,
                unit_of_measure="LO",
                psc_code=psc,
                naics_code=naics,
            )
        )

    return clins


def is_cots_candidate(clin: CLIN) -> bool:
    """Heuristic: is this CLIN likely a commercial off-the-shelf item?

    Checks:
    1. PSC code in supply ranges (numeric prefix = supplies)
    2. Keywords in description matching known COTS items
    3. Unit of measure is "EA" (each) indicating discrete items
    """
    # Check PSC code - supplies start with digits
    if clin.psc_code and clin.psc_code[0:1] in SUPPLY_PSC_PREFIXES:
        return True

    # Check unit of measure
    if clin.unit_of_measure.upper() in ("EA", "EACH"):
        desc_lower = clin.description.lower()
        for keyword in COTS_KEYWORDS:
            if keyword.lower() in desc_lower:
                return True

    # Check description keywords regardless
    desc_lower = clin.description.lower()
    strong_keywords = {"connector", "cable", "tablet", "laptop", "computer", "circuit breaker"}
    for keyword in strong_keywords:
        if keyword in desc_lower:
            return True

    return False


def clean_description(description: str) -> str:
    """Remove government procurement jargon to produce a cleaner search query.

    Strips NSNs, CAGE codes, MIL-SPEC refs, and other gov-specific terms.
    """
    cleaned = description
    for pattern in GOV_JARGON_PATTERNS:
        cleaned = re.sub(pattern, " ", cleaned, flags=re.IGNORECASE)

    # Remove extra whitespace
    cleaned = re.sub(r"\s+", " ", cleaned).strip()

    # Remove leading/trailing punctuation
    cleaned = cleaned.strip(",-;:.()")

    return cleaned
