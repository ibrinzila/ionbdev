---
schema: agent-companies/v1
kind: skill
slug: pr-review
name: "Domain Registration PR Review"
description: "When you need to review a domain registration pull request — validates JSON, DNS records, ownership, abuse indicators, and provides constructive feedback."
version: "1.0.0"
tags:
  - pr-review
  - domains
  - community
  - quality
---

# Domain Registration PR Review

Review domain registration PRs for correctness, policy compliance, and community standards.

## When to Use

- A new domain registration PR is opened
- A domain modification PR needs review
- Checking for abuse or policy violations in registrations

## Process

1. Identify changed files in the PR
2. For each new/modified domain file:
   - Run `json-schema-check` skill
   - Run `dns-validation` skill
   - Run `abuse-detection` skill
3. Check ownership information:
   - GitHub username matches PR author (or has explanation)
   - No impersonation of known accounts
4. Review for community guidelines:
   - Domain name is appropriate
   - No trademark infringement
   - No offensive content
5. Provide feedback:
   - Welcome first-time contributors
   - Give specific, actionable fix instructions for issues
   - Approve if all checks pass

## Inputs

- PR diff (changed files)
- PR author information
- Existing domain files (for nested subdomain validation)

## Outputs

- Review decision: Approved / Changes Requested / Rejected
- Detailed checklist of checks performed
- Constructive feedback for any issues found
