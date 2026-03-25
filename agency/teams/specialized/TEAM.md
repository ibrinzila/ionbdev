---
schema: agent-companies/v1
kind: team
slug: specialized
name: "Specialized Division"
description: "Deep expertise for unique challenges. Multi-agent orchestration, DNS migration, API design, and tool-specific experts."
version: "1.0.0"
tags:
  - specialized
  - dns
  - dnscontrol
  - github-actions
  - api
metadata:
  motto: "Deep expertise for unique challenges."
  max_autonomous_risk: medium
---

# Specialized Division

The Specialized Division provides deep domain expertise for unique, cross-cutting challenges.

## Manager

Multi-Agent Orchestrator — coordinates complex cross-functional tasks across divisions.

## Agents

- [Multi-Agent Orchestrator](../../specialized/specialized-multi-agent-orchestrator.md) — Agent coordination, task decomposition
- [DNS Migration Expert](../../specialized/specialized-dns-migration-expert.md) — Zone transfers, zero-downtime migrations
- [API Designer](../../specialized/specialized-api-designer.md) — REST/GraphQL API design, documentation
- [GitHub Actions Expert](../../specialized/specialized-github-actions-expert.md) — CI/CD workflows, automation
- [DNSControl Expert](../../specialized/specialized-dnscontrol-expert.md) — DNSControl config, zone management
- [JSON Schema Validator](../../specialized/specialized-json-schema-validator.md) — JSON schema, validation pipelines

## Skills Used

- dns-validation
- json-schema-check
- cloudflare-api
- git-operations

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: publish_dns, modify_dnsconfig
- Requires approval for: bulk_dns_update, delete_zone
