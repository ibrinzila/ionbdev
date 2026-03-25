---
schema: agent-companies/v1
kind: project
slug: domain-registration-v2
name: "Domain Registration v2"
description: "Redesign the domain registration flow with a web-based form, real-time validation, and automated PR creation — reducing the GitHub literacy barrier."
version: "1.0.0"
tags:
  - registration
  - ux
  - frontend
  - api
metadata:
  priority: P1
  quarter: Q2-2026
  status: planning
---

# Domain Registration v2

## Goal

Reduce domain registration time from 45 minutes to 5 minutes by replacing the manual JSON-file-and-PR workflow with a web-based registration form.

## Problem

73% of abandoned registrations happen at the JSON creation step. Users need to:
1. Fork the repo
2. Create a JSON file with the correct schema
3. Submit a PR
4. Wait for review and merge

This requires GitHub literacy that many developers (especially beginners) don't have.

## Solution

Build a web-based registration form that:
- Validates domain availability in real-time
- Guides users through DNS record selection
- Auto-generates the JSON file
- Creates the PR automatically via GitHub API

## Teams Involved

- **Product**: Requirements, prioritization, success metrics
- **Design**: UX research, form design, accessibility
- **Engineering**: Frontend form, backend API, GitHub integration
- **Quality**: Testing, accessibility audit, performance benchmarking
- **Community**: Documentation updates, contributor communication

## Starter Tasks

- [user-research](../../tasks/user-research/TASK.md) — Research current registration pain points
- [form-design](../../tasks/form-design/TASK.md) — Design the registration form UX
- [api-design](../../tasks/api-design/TASK.md) — Design the registration API

## Success Metrics

- Registration completion rate: 55% → 85%
- Time to first domain: 45 min → 5 min
- Support questions about JSON format: -80%
