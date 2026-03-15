"""Command-line interface for Pentagon Contract Analyzer."""

from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from pentagon_analyzer.config import Config


def main(argv: list[str] | None = None) -> None:
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        prog="pentagon-analyzer",
        description="Analyze Pentagon procurement data to find overpriced contracts",
    )
    parser.add_argument(
        "--env", default=".env", help="Path to .env file (default: .env)"
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true", help="Enable debug logging"
    )

    subparsers = parser.add_subparsers(dest="command", help="Available commands")

    # analyze - full pipeline
    p_analyze = subparsers.add_parser("analyze", help="Full analysis pipeline")
    p_analyze.add_argument("--agency", default="Department of Defense", help="Awarding agency name")
    p_analyze.add_argument("--naics", nargs="*", help="NAICS codes to filter")
    p_analyze.add_argument("--psc", nargs="*", help="PSC codes to filter")
    p_analyze.add_argument("--min-amount", type=float, help="Minimum award amount")
    p_analyze.add_argument("--max-amount", type=float, help="Maximum award amount")
    p_analyze.add_argument("--min-markup", type=float, default=10.0, help="Minimum markup ratio to flag (default: 10)")
    p_analyze.add_argument("--limit", type=int, default=100, help="Max contracts to analyze")
    p_analyze.add_argument("--output", nargs="*", default=["json", "csv", "html"], choices=["json", "csv", "html", "pptx"], help="Output formats")
    p_analyze.add_argument("--department-code", help="SAM.gov department code (e.g., 9700 for DOD)")

    # ingest - just pull data
    p_ingest = subparsers.add_parser("ingest", help="Pull and cache contract data")
    p_ingest.add_argument("--agency", default="Department of Defense")
    p_ingest.add_argument("--naics", nargs="*")
    p_ingest.add_argument("--psc", nargs="*")
    p_ingest.add_argument("--limit", type=int, default=100)
    p_ingest.add_argument("--department-code")

    # match - match against commercial pricing
    p_match = subparsers.add_parser("match", help="Match cached contracts against commercial pricing")
    p_match.add_argument("--min-markup", type=float, default=10.0)

    # score - vendor scoring + ML
    p_score = subparsers.add_parser("score", help="Run vendor scoring and ML ranking")

    # report - generate output
    p_report = subparsers.add_parser("report", help="Generate reports from cached analysis")
    p_report.add_argument("--output", nargs="*", default=["json", "csv", "html"], choices=["json", "csv", "html", "pptx"])

    # propose - generate CMMC proposal
    p_propose = subparsers.add_parser("propose", help="Generate CMMC proposal templates")
    p_propose.add_argument("--company", required=True, help="Company name")
    p_propose.add_argument("--uei", default="", help="Company UEI")
    p_propose.add_argument("--cage", default="", help="Company CAGE code")
    p_propose.add_argument("--level", type=int, choices=[1, 2], default=2, help="CMMC level (default: 2)")
    p_propose.add_argument("--contract-id", help="Target contract award ID for bid response")

    # import-csv - import commercial pricing
    p_csv = subparsers.add_parser("import-csv", help="Import commercial pricing from CSV")
    p_csv.add_argument("csv_file", type=Path, help="Path to CSV with columns: part_number, manufacturer, description, unit_price")

    args = parser.parse_args(argv)

    # Setup logging
    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)-8s %(name)s: %(message)s",
    )

    if not args.command:
        parser.print_help()
        sys.exit(1)

    config = Config.from_env(args.env)

    if args.command == "analyze":
        _cmd_analyze(config, args)
    elif args.command == "ingest":
        _cmd_ingest(config, args)
    elif args.command == "match":
        _cmd_match(config, args)
    elif args.command == "score":
        _cmd_score(config, args)
    elif args.command == "report":
        _cmd_report(config, args)
    elif args.command == "propose":
        _cmd_propose(config, args)
    elif args.command == "import-csv":
        _cmd_import_csv(config, args)


def _cmd_analyze(config: Config, args: argparse.Namespace) -> None:
    """Full analysis pipeline: ingest -> match -> score -> report."""
    from pentagon_analyzer.cache.store import CacheStore
    from pentagon_analyzer.clients.digikey import DigiKeyClient
    from pentagon_analyzer.clients.mouser import MouserClient
    from pentagon_analyzer.clients.sam_contracts import SAMContractsClient
    from pentagon_analyzer.clients.sam_entities import SAMEntitiesClient
    from pentagon_analyzer.clients.scraper import PriceScraper
    from pentagon_analyzer.clients.usaspending import USASpendingClient
    from pentagon_analyzer.matching.clin_parser import is_cots_candidate, parse_clins_from_usaspending
    from pentagon_analyzer.matching.price_comparator import analyze_markup
    from pentagon_analyzer.matching.product_matcher import ProductMatcher
    from pentagon_analyzer.models.analysis import AnalysisSummary, FlaggedContract
    from pentagon_analyzer.reports.generator import ReportGenerator
    from pentagon_analyzer.scoring.features import build_feature_matrix
    from pentagon_analyzer.scoring.model import UndercutRanker
    from pentagon_analyzer.scoring.vendor_scorer import VendorScorer

    logger = logging.getLogger("analyze")

    cache = CacheStore(config.cache_dir / "cache.db", config.cache_ttl_hours)

    # Initialize clients
    usa_client = USASpendingClient(cache=cache, max_retries=config.max_retries)

    sam_contracts = None
    sam_entities = None
    if config.has_sam:
        sam_contracts = SAMContractsClient(config.sam_api_key, cache=cache, max_retries=config.max_retries)
        sam_entities = SAMEntitiesClient(config.sam_api_key, cache=cache, max_retries=config.max_retries)

    digikey = None
    if config.has_digikey:
        digikey = DigiKeyClient(config.digikey_client_id, config.digikey_client_secret, cache=cache)

    mouser = None
    if config.has_mouser:
        mouser = MouserClient(config.mouser_api_key, cache=cache)

    matcher = ProductMatcher(digikey=digikey, mouser=mouser, scraper=PriceScraper())

    # 1. Ingest contracts from USAspending
    logger.info("Ingesting contracts from USAspending.gov...")
    awards_raw = list(usa_client.search_contract_awards(
        agency_name=args.agency,
        naics_codes=args.naics,
        psc_codes=args.psc,
        min_amount=args.min_amount,
        max_amount=args.max_amount,
        page_size=min(args.limit, 100),
    ))[:args.limit]

    awards = [usa_client.parse_award(r) for r in awards_raw]
    logger.info("Retrieved %d contract awards", len(awards))

    # 2. Parse CLINs and match against commercial pricing
    logger.info("Matching against commercial pricing...")
    all_analyses = []
    for award in awards:
        clins = parse_clins_from_usaspending(
            {**awards_raw[awards.index(award)], "psc_hierarchy": {"psc_code": award.psc_code}}
            if awards.index(award) < len(awards_raw)
            else {}
        )
        for clin in clins:
            if not is_cots_candidate(clin):
                continue
            match = matcher.best_match(clin)
            analysis = analyze_markup(
                clin, award.award_id, award.recipient_name,
                match, threshold=args.min_markup,
            )
            all_analyses.append((award, analysis))

    # 3. Build flagged contracts
    flagged_map: dict[str, FlaggedContract] = {}
    for award, analysis in all_analyses:
        if not analysis.flagged:
            continue
        if award.award_id not in flagged_map:
            flagged_map[award.award_id] = FlaggedContract(
                award_id=award.award_id,
                piid=award.piid,
                recipient_name=award.recipient_name,
                recipient_uei=award.recipient_uei,
                awarding_agency=award.awarding_agency,
                total_award_amount=award.award_amount,
                flagged_clins=[],
                total_overspend=0,
                max_markup_ratio=0,
                vendor_score=None,
                ease_of_undercut=0,
                competitor_count=0,
            )
        fc = flagged_map[award.award_id]
        fc.flagged_clins.append(analysis)
        fc.total_overspend += analysis.total_overspend or 0
        if analysis.markup_ratio and analysis.markup_ratio > fc.max_markup_ratio:
            fc.max_markup_ratio = analysis.markup_ratio

    # 4. Score vendors and rank
    if sam_entities:
        scorer = VendorScorer(sam_entities)
        for fc in flagged_map.values():
            if fc.recipient_uei:
                naics = fc.flagged_clins[0].clin.naics_code if fc.flagged_clins else ""
                vs = scorer.score_vendor(fc.recipient_uei, naics)
                fc.vendor_score = vs
                fc.competitor_count = vs.competitors_in_naics

    # 5. ML ranking
    ranker = UndercutRanker()
    flagged_list = sorted(flagged_map.values(), key=lambda f: -f.max_markup_ratio)

    if all_analyses:
        features_df = build_feature_matrix(
            [a for _, a in all_analyses if a.flagged],
            {fc.recipient_uei: fc.vendor_score for fc in flagged_list if fc.vendor_score},
        )
        if not features_df.empty:
            scores = ranker.predict(features_df)
            for i, fc in enumerate(flagged_list):
                if i < len(scores):
                    fc.ease_of_undercut = float(scores[i])

    # 6. Build summary
    summary = AnalysisSummary(
        total_contracts_analyzed=len(awards),
        total_clins_analyzed=len(all_analyses),
        contracts_flagged=len(flagged_map),
        clins_flagged=sum(len(fc.flagged_clins) for fc in flagged_map.values()),
        total_potential_savings=sum(fc.total_overspend for fc in flagged_map.values()),
        flagged_contracts=flagged_list,
        contracts_above_50x=sum(1 for fc in flagged_list if fc.max_markup_ratio >= 50),
        contracts_above_100x=sum(1 for fc in flagged_list if fc.max_markup_ratio >= 100),
        single_supplier_count=sum(1 for fc in flagged_list if fc.competitor_count <= 1),
    )

    # 7. Generate reports
    reporter = ReportGenerator(config.output_dir)
    outputs = reporter.generate(summary, formats=args.output)

    logger.info("Analysis complete!")
    logger.info("  Contracts analyzed: %d", summary.total_contracts_analyzed)
    logger.info("  Contracts flagged (10x+): %d", summary.contracts_flagged)
    logger.info("  Above 50x: %d", summary.contracts_above_50x)
    logger.info("  Total potential savings: $%,.0f", summary.total_potential_savings)
    for fmt, path in outputs.items():
        logger.info("  %s report: %s", fmt.upper(), path)


def _cmd_ingest(config: Config, args: argparse.Namespace) -> None:
    """Pull and cache contract data."""
    from pentagon_analyzer.cache.store import CacheStore
    from pentagon_analyzer.clients.usaspending import USASpendingClient

    logger = logging.getLogger("ingest")
    cache = CacheStore(config.cache_dir / "cache.db", config.cache_ttl_hours)
    client = USASpendingClient(cache=cache, max_retries=config.max_retries)

    awards = list(client.search_contract_awards(
        agency_name=args.agency,
        naics_codes=args.naics,
        psc_codes=args.psc,
        page_size=min(args.limit, 100),
    ))[:args.limit]

    logger.info("Cached %d contract awards", len(awards))


def _cmd_match(config: Config, args: argparse.Namespace) -> None:
    """Match cached contracts against commercial pricing."""
    logger = logging.getLogger("match")
    logger.info("Match command - use 'analyze' for full pipeline")


def _cmd_score(config: Config, args: argparse.Namespace) -> None:
    """Run vendor scoring and ML ranking."""
    logger = logging.getLogger("score")
    logger.info("Score command - use 'analyze' for full pipeline")


def _cmd_report(config: Config, args: argparse.Namespace) -> None:
    """Generate reports from cached analysis."""
    logger = logging.getLogger("report")
    logger.info("Report command - use 'analyze' for full pipeline")


def _cmd_propose(config: Config, args: argparse.Namespace) -> None:
    """Generate CMMC proposal templates."""
    from pentagon_analyzer.proposals.proposal_builder import ProposalBuilder

    logger = logging.getLogger("propose")
    builder = ProposalBuilder(config.output_dir)

    outputs = builder.generate_proposal(
        company_name=args.company,
        company_uei=args.uei,
        company_cage=args.cage,
        cmmc_level=args.level,
    )

    for doc_type, path in outputs.items():
        logger.info("Generated %s: %s", doc_type, path)


def _cmd_import_csv(config: Config, args: argparse.Namespace) -> None:
    """Import commercial pricing from CSV."""
    from pentagon_analyzer.matching.product_matcher import load_csv_prices

    logger = logging.getLogger("import-csv")
    prices = load_csv_prices(args.csv_file)
    logger.info("Loaded %d pricing entries from %s", len(prices), args.csv_file)
