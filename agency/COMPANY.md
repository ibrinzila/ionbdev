---
schema: agent-companies/v1
kind: company
slug: is-a-dev-agency
name: "is-a.dev Agency"
description: "A complete AI agency for the is-a.dev free developer domain service. 63 specialized agents across 13 divisions — from DNS specialists to design whimsy injectors, governed by a policy-enforced control plane."
version: "1.0.0"
license: MIT
authors:
  - name: is-a.dev contributors
    url: https://github.com/is-a-dev/register
tags:
  - dns
  - domains
  - open-source
  - developer-tools
  - cloudflare
  - github-actions
metadata:
  sources:
    - url: https://github.com/is-a-dev/register
      description: "is-a.dev domain registration repository"
    - url: https://github.com/msitarzewski/agency-agents
      description: "The Agency — inspiration for agent structure"
    - url: https://agentcompanies.io
      description: "Agent Companies protocol specification"
---

# is-a.dev Agency

A complete AI agency for the **is-a.dev** free developer domain service — managing 6,000+ domain registrations, DNS infrastructure, community contributions, and project growth.

## Mission

Provide specialized AI agent expertise for every aspect of running and scaling the is-a.dev open-source domain service, governed by a control plane that ensures policy compliance, audit accountability, and rollback safety.

## Boundaries

This agency covers:
- DNS management and Cloudflare operations
- Domain registration validation and publishing
- Community PR review and contributor support
- Security, abuse prevention, and compliance
- Product strategy, marketing, and growth
- Infrastructure maintenance and CI/CD automation
- Quality assurance and performance testing

This agency does NOT cover:
- Domain registrar operations (is-a.dev uses DNSControl, not a registrar)
- Financial transactions or billing
- Legal advice (compliance agents provide guidance, not legal counsel)

## Defaults

- **Default risk level**: When an action is unknown, classify as `high`
- **Default approval**: Human approval required for anything touching DNS production
- **Default rollback**: Every write action must have a defined rollback path
- **Default confidence threshold**: 0.70 minimum for autonomous execution
- **Default communication style**: Constructive, specific, action-oriented

## Governance

All agent actions are governed by the [Control Plane](control-plane/README.md):
- **Policy**: [`control-plane/policy.yml`](control-plane/policy.yml)
- **Risk classification**: 4 levels (low → medium → high → critical)
- **Role contracts**: Each division has explicit autonomous risk limits
- **Audit logging**: Every action recorded with structured JSON
- **Circuit breakers**: Automatic halt on failure rate spikes

## Teams

- [Engineering](teams/engineering/TEAM.md) — 13 agents
- [Design](teams/design/TEAM.md) — 7 agents
- [Operations](teams/operations/TEAM.md) — 4 agents
- [Community](teams/community/TEAM.md) — 5 agents
- [Security](teams/security/TEAM.md) — 3 agents
- [Quality](teams/quality/TEAM.md) — 3 agents
- [Marketing](teams/marketing/TEAM.md) — 4 agents
- [Product](teams/product/TEAM.md) — 4 agents
- [Support](teams/support/TEAM.md) — 3 agents
- [Strategy](teams/strategy/TEAM.md) — 3 agents
- [Project Management](teams/project-management/TEAM.md) — 3 agents
- [Specialized](teams/specialized/TEAM.md) — 6 agents
- [Control Plane](teams/control-plane/TEAM.md) — 5 agents
- [World Model](teams/world-model/TEAM.md) — 6 agents (autonomous, event-driven)

## World Model

The autonomous intelligence layer — not triggered by prompts, but by events:
- **Event monitors** watch GitHub, DNS, community, and security feeds continuously
- **Decision engine** classifies, prioritizes, and routes to the right agents
- **Coordination layer** manages the new bottleneck: review, prioritization, and operating design

See [World Model documentation](world-model/README.md) for the full architecture.

## Skills

Reusable capabilities shared across agents:
- [dns-validation](skills/dns-validation/SKILL.md) — Validate DNS records against RFC standards
- [json-schema-check](skills/json-schema-check/SKILL.md) — Validate domain JSON files
- [pr-review](skills/pr-review/SKILL.md) — Review domain registration PRs
- [cloudflare-api](skills/cloudflare-api/SKILL.md) — Interact with Cloudflare DNS API
- [git-operations](skills/git-operations/SKILL.md) — Safe git workflow operations
- [incident-response](skills/incident-response/SKILL.md) — Structured incident management
- [abuse-detection](skills/abuse-detection/SKILL.md) — Detect malicious domain registrations
