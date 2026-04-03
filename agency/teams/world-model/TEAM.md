---
schema: agent-companies/v1
kind: team
slug: world-model
name: "World Model"
description: "The company's nervous system. Autonomous monitors watch event sources, a decision engine prioritizes and routes, and a coordination layer manages the new bottleneck — review, prioritization, and operating design."
version: "1.0.0"
tags:
  - autonomous
  - monitoring
  - decision-engine
  - coordination
  - world-model
metadata:
  motto: "A live picture of what is happening and what needs to happen next."
  max_autonomous_risk: medium
  trigger: event-driven (not human-prompted)
---

# World Model

The World Model is the autonomous intelligence layer. Unlike other divisions, these agents are not triggered by human prompts — they are triggered by events, monitoring signals, and the live state of the company.

This is the beginning of a company world model: **a live picture of what is happening inside is-a.dev and what needs to happen next.**

## Manager

Decision Engine — the central brain that receives all signals and makes routing decisions.

## Agents

### Event Source Monitors (always-on, read-only)
- [GitHub Monitor](../../world-model/world-model-github-monitor.md) — Issues, PRs, CI, security alerts, contributor activity
- [DNS Health Monitor](../../world-model/world-model-dns-monitor.md) — Resolution, propagation, DNSSEC, subdomain takeover
- [Community Sentiment Monitor](../../world-model/world-model-community-monitor.md) — Discord, sentiment, support queues, growth
- [Security Monitor](../../world-model/world-model-security-monitor.md) — Phishing, abuse, CVEs, threat intelligence

### Decision & Coordination
- [Decision Engine](../../world-model/world-model-decision-engine.md) — Central intelligence: classify, prioritize, route, act or escalate
- [Coordination Layer](../../world-model/world-model-coordination-layer.md) — Cross-team dependencies, review capacity, operating design

## How It Works

```
Events happen → Monitors detect → Decision Engine evaluates →
Control Plane authorizes → Agent divisions execute
```

1. **Monitors** watch event sources continuously (GitHub, DNS, Discord, security feeds)
2. **Signals** are generated when events match patterns or thresholds breach
3. **Decision Engine** classifies, prioritizes, and routes to the best agent
4. **Control Plane** authorizes the action (same governance as always)
5. **Agents** execute with their specialized expertise
6. **Coordination Layer** manages cross-team dependencies and review queues

## Autonomy Levels

| Level | When | Example |
|-------|------|---------|
| Full auto | Low risk, high confidence | Label PRs, run validation |
| Auto-investigate, human approves | Medium risk | DNS spike → investigate → propose fix |
| Auto-investigate, auto-fix if low risk | Medium risk, safe fix | Update minor dependency if tests pass |
| Human required | High/critical risk | Production DNS changes, user bans |

## Governance

The world model is event-driven but still governed:
- All actions pass through the control plane
- Decision Engine cannot bypass policy
- Monitors are read-only (cannot modify systems)
- Human escalation is mandatory for high-risk decisions
