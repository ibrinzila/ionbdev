---
schema: agent-companies/v1
kind: team
slug: project-management
name: "Project Management Division"
description: "Keeping everything on track. Coordination, operations, and experiment tracking."
version: "1.0.0"
tags:
  - project-management
  - coordination
  - operations
metadata:
  motto: "Keeping everything on track."
  max_autonomous_risk: low
---

# Project Management Division

The Project Management Division coordinates cross-team efforts, optimizes processes, and tracks experiments.

## Manager

Project Coordinator — owns timelines, milestones, and delivery tracking.

## Agents

- [Project Coordinator](../../project-management/pm-project-coordinator.md) — Cross-team coordination, timelines
- [Operations Manager](../../project-management/pm-operations-manager.md) — Process optimization, efficiency
- [Experiment Tracker](../../project-management/pm-experiment-tracker.md) — Experiment design, hypothesis testing

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **low**
- Allowed medium-risk: add_label, assign_reviewer
