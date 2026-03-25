---
schema: agent-companies/v1
kind: team
slug: engineering
name: "Engineering Division"
description: "Building the future, one commit at a time. Frontend, backend, DevOps, security, AI, and infrastructure engineering."
version: "1.0.0"
tags:
  - engineering
  - development
  - infrastructure
metadata:
  motto: "Building the future, one commit at a time."
  max_autonomous_risk: medium
---

# Engineering Division

The Engineering Division handles all software development — from frontend UI to backend APIs, DevOps automation to security hardening.

## Manager

Senior Developer — makes architecture decisions, reviews complex implementations, and mentors the team.

## Agents

- [Frontend Developer](../../engineering/engineering-frontend-developer.md) — React/Vue/Angular, UI, performance
- [Backend Architect](../../engineering/engineering-backend-architect.md) — API design, databases, scalability
- [Mobile App Builder](../../engineering/engineering-mobile-app-builder.md) — iOS/Android, React Native, Flutter
- [AI Engineer](../../engineering/engineering-ai-engineer.md) — ML models, AI integration
- [DevOps Automator](../../engineering/engineering-devops-automator.md) — CI/CD, infrastructure automation
- [Rapid Prototyper](../../engineering/engineering-rapid-prototyper.md) — Fast POC development, MVPs
- [Senior Developer](../../engineering/engineering-senior-developer.md) — Complex implementations, architecture
- [Security Engineer](../../engineering/engineering-security-engineer.md) — Threat modeling, secure code review
- [Code Reviewer](../../engineering/engineering-code-reviewer.md) — Quality assurance, best practices
- [Database Optimizer](../../engineering/engineering-database-optimizer.md) — Schema design, query optimization
- [Software Architect](../../engineering/engineering-software-architect.md) — System design, technology selection
- [Technical Writer](../../engineering/engineering-technical-writer.md) — Documentation, API references
- [Git Workflow Master](../../engineering/engineering-git-workflow-master.md) — Git workflows, version control

## Skills Used

- dns-validation
- json-schema-check
- git-operations

## Governance

Per [policy.yml](../../control-plane/policy.yml):
- Max autonomous risk: **medium**
- Allowed high-risk: create_pr, run_tests
- Forbidden: publish_dns, modify_cloudflare
