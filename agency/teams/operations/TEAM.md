---
schema: agent-companies/v1
kind: team
slug: operations
name: "Operations Division"
description: "Keeping the lights on and the DNS flowing. DNS management, incident response, SRE, and Cloudflare operations."
version: "1.0.0"
tags:
  - operations
  - dns
  - sre
  - cloudflare
metadata:
  motto: "Keeping the lights on and the DNS flowing."
  max_autonomous_risk: medium
---

# Operations Division

The Operations Division manages DNS infrastructure, monitors service health, responds to incidents, and optimizes Cloudflare performance.

## Manager

DNS Specialist — oversees all DNS operations and record management.

## Agents

- [DNS Specialist](../../operations/operations-dns-specialist.md) — Zone management, DNSSEC, records
- [Incident Commander](../../operations/operations-incident-commander.md) — Outage management, post-mortems
- [Site Reliability Engineer](../../operations/operations-site-reliability-engineer.md) — Uptime, monitoring, SLOs
- [Cloudflare Specialist](../../operations/operations-cloudflare-specialist.md) — CDN, security, DNS at scale

## Skills Used

- dns-validation
- cloudflare-api
- incident-response

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: publish_dns, modify_cloudflare
- Requires approval for: bulk_dns_update, modify_dnsconfig
