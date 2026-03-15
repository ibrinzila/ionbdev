"""Report generation orchestrator - JSON, CSV, HTML, PPTX output."""

from __future__ import annotations

import logging
from datetime import datetime
from pathlib import Path

from jinja2 import Environment, FileSystemLoader

from pentagon_analyzer.models.analysis import AnalysisSummary
from pentagon_analyzer.reports.formatters import to_csv, to_json

logger = logging.getLogger(__name__)

TEMPLATE_DIR = Path(__file__).parent / "templates"


class ReportGenerator:
    """Generate analysis reports in multiple formats."""

    def __init__(self, output_dir: Path):
        self._output_dir = output_dir
        self._output_dir.mkdir(parents=True, exist_ok=True)
        self._jinja = Environment(
            loader=FileSystemLoader(str(TEMPLATE_DIR)),
            autoescape=True,
        )

    def generate(
        self,
        summary: AnalysisSummary,
        formats: list[str] | None = None,
    ) -> dict[str, Path]:
        """Generate reports in requested formats.

        Args:
            summary: Analysis results to report on.
            formats: List of formats: "json", "csv", "html", "pptx".
                     Defaults to all.

        Returns:
            Dict mapping format to output file path.
        """
        formats = formats or ["json", "csv", "html"]
        outputs: dict[str, Path] = {}
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

        if "json" in formats:
            path = self._output_dir / f"report_{timestamp}.json"
            path.write_text(to_json(summary))
            outputs["json"] = path
            logger.info("JSON report: %s", path)

        if "csv" in formats:
            path = self._output_dir / f"report_{timestamp}.csv"
            path.write_text(to_csv(summary))
            outputs["csv"] = path
            logger.info("CSV report: %s", path)

        if "html" in formats:
            path = self._output_dir / f"report_{timestamp}.html"
            self._generate_html(summary, path)
            outputs["html"] = path
            logger.info("HTML report: %s", path)

        if "pptx" in formats:
            path = self._output_dir / f"pitch_deck_{timestamp}.pptx"
            self._generate_pptx(summary, path)
            outputs["pptx"] = path
            logger.info("Pitch deck: %s", path)

        return outputs

    def _generate_html(self, summary: AnalysisSummary, path: Path) -> None:
        """Render HTML report from Jinja2 template."""
        template = self._jinja.get_template("report.html.j2")
        html = template.render(
            summary=summary,
            flagged_contracts=summary.flagged_contracts,
            generated_at=datetime.now().strftime("%Y-%m-%d %H:%M UTC"),
        )
        path.write_text(html)

    def _generate_pptx(self, summary: AnalysisSummary, path: Path) -> None:
        """Generate PPTX pitch deck with executive summary."""
        from pptx import Presentation
        from pptx.util import Inches, Pt
        from pptx.enum.text import PP_ALIGN

        prs = Presentation()
        prs.slide_width = Inches(13.333)
        prs.slide_height = Inches(7.5)

        # Title slide
        slide = prs.slides.add_slide(prs.slide_layouts[0])
        slide.shapes.title.text = "Pentagon Contract Analysis"
        slide.placeholders[1].text = (
            f"${summary.total_potential_savings:,.0f} in potential undercuts identified\n"
            f"{summary.contracts_flagged} contracts flagged at 10x+ markup"
        )

        # Summary slide
        slide = prs.slides.add_slide(prs.slide_layouts[1])
        slide.shapes.title.text = "Executive Summary"
        body = slide.placeholders[1]
        tf = body.text_frame
        tf.text = ""

        bullets = [
            f"Analyzed {summary.total_contracts_analyzed} contracts, {summary.total_clins_analyzed} line items",
            f"Flagged {summary.contracts_flagged} contracts with 10x+ commercial markup",
            f"{summary.contracts_above_50x} contracts above 50x markup",
            f"${summary.total_potential_savings:,.0f} total potential savings",
            f"{summary.single_supplier_count} single-supplier contracts (zero competition)",
        ]
        for bullet in bullets:
            p = tf.add_paragraph()
            p.text = bullet
            p.font.size = Pt(18)

        # Top findings slide
        slide = prs.slides.add_slide(prs.slide_layouts[1])
        slide.shapes.title.text = "Top Findings by Markup Ratio"
        body = slide.placeholders[1]
        tf = body.text_frame
        tf.text = ""

        for fc in summary.flagged_contracts[:10]:
            for ma in fc.flagged_clins[:1]:
                p = tf.add_paragraph()
                p.text = (
                    f"{ma.clin.description[:50]}... — "
                    f"${ma.clin.unit_price:,.2f} gov vs "
                    f"${ma.commercial_match.commercial_unit_price:,.2f} commercial"
                    f" ({ma.markup_ratio:.0f}x)"
                    if ma.commercial_match and ma.markup_ratio
                    else f"{fc.award_id} — ${fc.total_award_amount:,.0f}"
                )
                p.font.size = Pt(14)

        # Opportunity slide
        slide = prs.slides.add_slide(prs.slide_layouts[1])
        slide.shapes.title.text = "Opportunity Assessment"
        body = slide.placeholders[1]
        tf = body.text_frame
        tf.text = ""

        lines = [
            "Undercutting by 40% on flagged contracts would still leave 6x+ margins",
            f"{summary.single_supplier_count} contracts have zero competition",
            "Lowest-hanging fruit: high-markup, single-supplier commodity items",
            "Recommended next steps: SAM.gov registration, CMMC L2 certification",
        ]
        for line in lines:
            p = tf.add_paragraph()
            p.text = line
            p.font.size = Pt(16)

        prs.save(str(path))
