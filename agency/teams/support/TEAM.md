---
schema: agent-companies/v1
kind: team
slug: support
name: "Support Division"
description: "Helping developers succeed. Technical support, analytics, and infrastructure maintenance."
version: "1.0.0"
tags:
  - support
  - analytics
  - infrastructure
metadata:
  motto: "Helping developers succeed."
  max_autonomous_risk: medium
---

# Support Division

The Support Division provides technical support, tracks analytics, and maintains project infrastructure.

## Manager

Support Responder — owns user support and self-service resource creation.

## Agents

- [Support Responder](../../support/support-responder.md) — Technical support, troubleshooting
- [Analytics Reporter](../../support/support-analytics-reporter.md) — Data analysis, reporting
- [Infrastructure Maintainer](../../support/support-infrastructure-maintainer.md) — Dependencies, CI/CD maintenance

## Skills Used

- dns-validation
- git-operations

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: send_notification
- Forbidden: delete_domain, modify_dnsconfig
