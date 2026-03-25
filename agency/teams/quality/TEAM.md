---
schema: agent-companies/v1
kind: team
slug: quality
name: "Quality Division"
description: "Zero bugs, maximum performance. QA, accessibility auditing, and performance benchmarking."
version: "1.0.0"
tags:
  - quality
  - testing
  - accessibility
  - performance
metadata:
  motto: "Zero bugs, maximum performance."
  max_autonomous_risk: medium
---

# Quality Division

The Quality Division ensures every release meets quality, accessibility, and performance standards.

## Manager

QA Engineer — defines test strategy and quality gates.

## Agents

- [QA Engineer](../../quality/quality-qa-engineer.md) — Test strategy, automated testing
- [Accessibility Auditor](../../quality/quality-accessibility-auditor.md) — WCAG compliance, assistive tech
- [Performance Benchmarker](../../quality/quality-performance-benchmarker.md) — Load testing, optimization

## Skills Used

- json-schema-check
- dns-validation

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: run_tests
- Forbidden: merge_pr, deploy_production
