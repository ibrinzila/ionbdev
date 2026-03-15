"""Assemble CMMC 2.0 compliant proposal documents."""

from __future__ import annotations

import logging
from datetime import datetime
from pathlib import Path
from typing import Any

from jinja2 import Environment, FileSystemLoader

from pentagon_analyzer.models.analysis import AnalysisSummary, FlaggedContract
from pentagon_analyzer.proposals.cmmc_templates import (
    LEVEL_1_PRACTICES,
    LEVEL_2_DOMAINS,
)

logger = logging.getLogger(__name__)

TEMPLATE_DIR = Path(__file__).parent / "templates"


class ProposalBuilder:
    """Generate CMMC-compliant proposal documents from analysis results."""

    def __init__(self, output_dir: Path):
        self._output_dir = output_dir
        self._output_dir.mkdir(parents=True, exist_ok=True)
        self._jinja = Environment(
            loader=FileSystemLoader(str(TEMPLATE_DIR)),
            autoescape=True,
        )

    def generate_proposal(
        self,
        *,
        company_name: str,
        company_uei: str = "",
        company_cage: str = "",
        cmmc_level: int = 2,
        target_contract: FlaggedContract | None = None,
        summary: AnalysisSummary | None = None,
    ) -> dict[str, Path]:
        """Generate a full proposal package.

        Returns dict of document type to output path.
        """
        outputs: dict[str, Path] = {}
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

        # SSP
        ssp_path = self._output_dir / f"ssp_{timestamp}.html"
        self._generate_ssp(ssp_path, company_name=company_name, cmmc_level=cmmc_level)
        outputs["ssp"] = ssp_path

        # POA&M
        poam_path = self._output_dir / f"poam_{timestamp}.html"
        self._generate_poam(poam_path, company_name=company_name, cmmc_level=cmmc_level)
        outputs["poam"] = poam_path

        # Proposal
        if target_contract:
            prop_path = self._output_dir / f"proposal_{timestamp}.html"
            self._generate_proposal_doc(
                prop_path,
                company_name=company_name,
                company_uei=company_uei,
                company_cage=company_cage,
                cmmc_level=cmmc_level,
                contract=target_contract,
            )
            outputs["proposal"] = prop_path

        logger.info("Generated proposal package: %s", list(outputs.keys()))
        return outputs

    def _generate_ssp(
        self, path: Path, *, company_name: str, cmmc_level: int
    ) -> None:
        """Generate System Security Plan."""
        if cmmc_level == 1:
            domains = _l1_domains()
        else:
            domains = _l2_domains()

        template = self._jinja.get_template("ssp_template.j2")
        html = template.render(
            company_name=company_name,
            system_name=f"{company_name} IT System",
            prepared_date=datetime.now().strftime("%Y-%m-%d"),
            prepared_by="[Authorized Representative]",
            cmmc_level=cmmc_level,
            version="1.0",
            system_description="[Description of the information system boundary and components]",
            system_boundary="[Define the system boundary including all hardware, software, and network components]",
            information_types="Federal Contract Information (FCI)" + (", Controlled Unclassified Information (CUI)" if cmmc_level >= 2 else ""),
            operating_environment="[Describe physical and logical operating environment]",
            network_architecture="[Describe network topology and architecture]",
            data_flow="[Describe how data flows through the system]",
            personnel=[
                {"role": "System Owner", "name": "[Name]", "contact": "[Email/Phone]"},
                {"role": "Information System Security Officer (ISSO)", "name": "[Name]", "contact": "[Email/Phone]"},
                {"role": "System Administrator", "name": "[Name]", "contact": "[Email/Phone]"},
            ],
            domains=domains,
        )
        path.write_text(html)

    def _generate_poam(
        self, path: Path, *, company_name: str, cmmc_level: int
    ) -> None:
        """Generate Plan of Action & Milestones."""
        template = self._jinja.get_template("poam_template.j2")
        html = template.render(
            company_name=company_name,
            prepared_date=datetime.now().strftime("%Y-%m-%d"),
            cmmc_level=cmmc_level,
            poam_items=[],  # Empty template - user fills in
        )
        path.write_text(html)

    def _generate_proposal_doc(
        self,
        path: Path,
        *,
        company_name: str,
        company_uei: str,
        company_cage: str,
        cmmc_level: int,
        contract: FlaggedContract,
    ) -> None:
        """Generate bid response proposal."""
        # Calculate pricing at 60% of government price (40% undercut)
        line_items = []
        total_proposed = 0
        for ma in contract.flagged_clins:
            if ma.commercial_match and ma.markup_ratio:
                # Price at 60% of gov price = still ~6x commercial margin
                proposed_price = ma.clin.unit_price * 0.6
            else:
                proposed_price = ma.clin.unit_price * 0.7
            total = proposed_price * ma.clin.quantity
            total_proposed += total
            line_items.append({
                "clin": ma.clin.clin_number,
                "description": ma.clin.description[:80],
                "quantity": ma.clin.quantity,
                "unit_price": proposed_price,
                "total": total,
            })

        savings = contract.total_award_amount - total_proposed
        savings_pct = (savings / contract.total_award_amount * 100) if contract.total_award_amount else 0

        template = self._jinja.get_template("proposal_base.j2")
        html = template.render(
            company_name=company_name,
            company_uei=company_uei,
            company_cage=company_cage,
            solicitation_number=f"[Solicitation for {contract.piid}]",
            contract_number=contract.piid,
            naics_code=contract.flagged_clins[0].clin.naics_code if contract.flagged_clins else "",
            psc_code=contract.flagged_clins[0].clin.psc_code if contract.flagged_clins else "",
            submission_date=datetime.now().strftime("%Y-%m-%d"),
            cmmc_level=cmmc_level,
            savings_percentage=f"{savings_pct:.0f}",
            total_savings=savings,
            line_items=line_items,
            total_proposed=total_proposed,
        )
        path.write_text(html)


def _l1_domains() -> list[dict[str, Any]]:
    """Build domain structure for CMMC Level 1."""
    domains_map: dict[str, dict] = {}
    for p in LEVEL_1_PRACTICES:
        if p.domain not in domains_map:
            domains_map[p.domain] = {
                "id": p.id.split(".")[0],
                "name": p.domain,
                "description": f"CMMC Level 1 {p.domain} practices",
                "controls": [],
            }
        domains_map[p.domain]["controls"].append({
            "id": p.id,
            "description": p.description,
        })
    return list(domains_map.values())


def _l2_domains() -> list[dict[str, Any]]:
    """Build domain structure for CMMC Level 2."""
    domains = []
    for d in LEVEL_2_DOMAINS:
        controls = []
        for i in range(1, d["control_count"] + 1):
            controls.append({
                "id": f"{d['id']}.L2-3.x.{i}",
                "description": f"[NIST SP 800-171 control {d['id']}-{i:02d}]",
            })
        domains.append({
            "id": d["id"],
            "name": d["name"],
            "description": d["description"],
            "controls": controls,
        })
    return domains
