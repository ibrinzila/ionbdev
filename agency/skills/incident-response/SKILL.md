---
schema: agent-companies/v1
kind: skill
slug: incident-response
name: "Structured Incident Response"
description: "When a service disruption occurs — structured incident management with timeline tracking, communication, root cause analysis, and blameless post-mortems."
version: "1.0.0"
tags:
  - incident
  - response
  - postmortem
  - reliability
---

# Structured Incident Response

Manage service disruptions with structured processes, clear communication, and blameless post-mortems.

## When to Use

- DNS resolution failures detected
- Cloudflare API errors or outages
- CI/CD pipeline failures blocking deployments
- Bulk domain validation failures
- Any service disruption affecting users

## Process

1. **Detect**: Identify the issue (alert, user report, monitoring)
2. **Assess**: Determine severity (P1-P4) and blast radius
3. **Communicate**: Notify stakeholders with initial status
4. **Investigate**: Systematic troubleshooting with timeline
5. **Resolve**: Implement fix or workaround
6. **Verify**: Confirm service restored, monitor for recurrence
7. **Post-mortem**: Blameless review within 48 hours

## Severity Levels

- **P1**: Complete DNS resolution failure (all domains affected)
- **P2**: Partial outage (subset of domains, specific record types)
- **P3**: Degraded service (slow resolution, CI/CD failures)
- **P4**: Minor issue (cosmetic, documentation, non-blocking)

## Outputs

- Incident report with timeline
- Root cause analysis
- Action items with owners and deadlines
- Post-mortem document
