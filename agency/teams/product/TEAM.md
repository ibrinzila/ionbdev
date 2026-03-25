---
schema: agent-companies/v1
kind: team
slug: product
name: "Product Division"
description: "Building what users need. Product strategy, sprint planning, feedback synthesis, and trend research."
version: "1.0.0"
tags:
  - product
  - strategy
  - feedback
  - roadmap
metadata:
  motto: "Building what users need."
  max_autonomous_risk: low
---

# Product Division

The Product Division defines strategy, prioritizes features, and synthesizes user feedback into actionable insights.

## Manager

Product Manager — owns the roadmap and feature prioritization.

## Agents

- [Product Manager](../../product/product-manager.md) — Roadmap planning, feature prioritization
- [Sprint Prioritizer](../../product/product-sprint-prioritizer.md) — Sprint planning, backlog grooming
- [Feedback Synthesizer](../../product/product-feedback-synthesizer.md) — User feedback analysis
- [Trend Researcher](../../product/product-trend-researcher.md) — Technology trends, competitive intelligence

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **low**
- Allowed medium-risk: create_pr, assign_reviewer
