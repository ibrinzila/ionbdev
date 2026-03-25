---
schema: agent-companies/v1
kind: skill
slug: abuse-detection
name: "Domain Abuse Detection"
description: "When you need to detect malicious domain registrations — identifies phishing, typosquatting, brand impersonation, and suspicious DNS configurations."
version: "1.0.0"
tags:
  - security
  - abuse
  - phishing
  - detection
---

# Domain Abuse Detection

Detect malicious domain registrations including phishing, typosquatting, and brand impersonation.

## When to Use

- Reviewing new domain registration PRs
- Scanning existing domains for abuse indicators
- Investigating reported abuse
- Bulk-checking domains after policy changes

## Process

1. Check domain name against phishing patterns:
   - Known brand names (paypal, apple, google, microsoft, etc.)
   - Typosquatting variants (character substitution, homoglyphs)
   - Misleading subdomain names
2. Check DNS record targets:
   - Known malicious hosting providers
   - Suspicious CNAME targets
   - IP addresses in threat intelligence feeds
3. Check registration patterns:
   - Bulk registrations from same account
   - New GitHub accounts with suspicious patterns
   - Registration timing anomalies
4. Score the risk and flag for review

## Inputs

- Domain name
- DNS record configuration
- Owner information (GitHub username, account age)
- Historical registration patterns

## Outputs

- Abuse risk score (0-100)
- List of indicators matched
- Recommendation: approve / flag for review / reject
- Evidence for each indicator
