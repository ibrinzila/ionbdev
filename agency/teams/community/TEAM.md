---
schema: agent-companies/v1
kind: team
slug: community
name: "Community Division"
description: "Growing and nurturing the developer community. PR reviews, Discord moderation, documentation, and abuse prevention."
version: "1.0.0"
tags:
  - community
  - open-source
  - moderation
  - documentation
metadata:
  motto: "Growing and nurturing the developer community."
  max_autonomous_risk: medium
---

# Community Division

The Community Division manages contributor relationships, reviews domain registration PRs, maintains documentation, and prevents abuse.

## Manager

PR Reviewer — oversees contribution quality and community engagement.

## Agents

- [PR Reviewer](../../community/community-pr-reviewer.md) — Open-source contribution review
- [Discord Moderator](../../community/community-discord-moderator.md) — Community management, engagement
- [Documentation Curator](../../community/community-documentation-curator.md) — Knowledge base, onboarding
- [Social Media Manager](../../community/community-social-media-manager.md) — Developer social media, promotion
- [Abuse Prevention](../../community/community-abuse-prevention.md) — Phishing detection, spam prevention

## Skills Used

- pr-review
- json-schema-check
- abuse-detection

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: merge_pr, add_label
- Forbidden: modify_dnsconfig, deploy_production
