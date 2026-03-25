---
schema: agent-companies/v1
kind: team
slug: security
name: "Security Division"
description: "Protecting the community from threats. Threat intelligence, compliance, and penetration testing."
version: "1.0.0"
tags:
  - security
  - compliance
  - threat-intelligence
metadata:
  motto: "Protecting the community from threats."
  max_autonomous_risk: medium
---

# Security Division

The Security Division detects threats, ensures regulatory compliance, and tests security defenses.

## Manager

Threat Analyst — coordinates threat intelligence and security posture.

## Agents

- [Threat Analyst](../../security/security-threat-analyst.md) — DNS threat intelligence, attack patterns
- [Compliance Officer](../../security/security-compliance-officer.md) — Regulatory compliance, privacy
- [Penetration Tester](../../security/security-penetration-tester.md) — Security testing, vulnerability assessment

## Skills Used

- abuse-detection
- dns-validation

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: ban_user
- Requires approval for: revoke_access
