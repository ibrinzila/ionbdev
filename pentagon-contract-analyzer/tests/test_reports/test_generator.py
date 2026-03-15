"""Tests for report generation."""

from pathlib import Path

import pytest

from pentagon_analyzer.models.analysis import AnalysisSummary, FlaggedContract, MarkupAnalysis
from pentagon_analyzer.models.contract import CLIN
from pentagon_analyzer.models.pricing import CommercialMatch
from pentagon_analyzer.reports.formatters import to_csv, to_json
from pentagon_analyzer.reports.generator import ReportGenerator


@pytest.fixture
def summary(sample_flagged_contract):
    return AnalysisSummary(
        total_contracts_analyzed=100,
        total_clins_analyzed=350,
        contracts_flagged=5,
        clins_flagged=12,
        total_potential_savings=4200000.0,
        flagged_contracts=[sample_flagged_contract],
        contracts_above_50x=3,
        contracts_above_100x=1,
        single_supplier_count=2,
    )


def test_to_json(summary):
    result = to_json(summary)
    assert '"total_potential_savings": 4200000.0' in result
    assert '"contracts_flagged": 5' in result
    assert "ACME Defense Corp" in result


def test_to_csv(summary):
    result = to_csv(summary)
    lines = result.strip().split("\n")
    assert len(lines) == 2  # header + 1 data row
    assert "Award ID" in lines[0]
    assert "W52P1J-20-C-0042" in lines[1]
    assert "86.5" in lines[1]  # markup ratio


def test_html_report_generation(summary, tmp_path):
    gen = ReportGenerator(tmp_path)
    outputs = gen.generate(summary, formats=["html"])
    assert "html" in outputs
    html_path = outputs["html"]
    assert html_path.exists()
    content = html_path.read_text()
    assert "Pentagon Contract Analysis Report" in content
    assert "$4,200,000" in content
    assert "ACME Defense Corp" in content


def test_json_report_generation(summary, tmp_path):
    gen = ReportGenerator(tmp_path)
    outputs = gen.generate(summary, formats=["json"])
    assert "json" in outputs
    assert outputs["json"].exists()


def test_csv_report_generation(summary, tmp_path):
    gen = ReportGenerator(tmp_path)
    outputs = gen.generate(summary, formats=["csv"])
    assert "csv" in outputs
    assert outputs["csv"].exists()
