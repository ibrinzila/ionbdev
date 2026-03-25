---
schema: agent-companies/v1
kind: team
slug: control-plane
name: "Control Plane"
description: "Models suggest. The control plane decides. Policy enforcement, risk classification, audit logging, rollback safety, and circuit breaking."
version: "1.0.0"
tags:
  - governance
  - policy
  - security
  - audit
  - rollback
metadata:
  motto: "Models suggest. The control plane decides."
  max_autonomous_risk: high
---

# Control Plane

The Control Plane enforces policy on every agent action. It classifies risk, checks approvals, ensures rollback paths exist, logs everything, and halts operations when failures cascade.

## Manager

Policy Enforcer — the gatekeeper that authorizes or denies every action.

## Agents

- [Policy Enforcer](../../control-plane/control-plane-policy-enforcer.md) — Action authorization against policy
- [Audit Logger](../../control-plane/control-plane-audit-logger.md) — Structured logging of all actions
- [Rollback Guardian](../../control-plane/control-plane-rollback-guardian.md) — Rollback verification and execution
- [Risk Classifier](../../control-plane/control-plane-risk-classifier.md) — Context-aware risk assessment
- [Circuit Breaker](../../control-plane/control-plane-circuit-breaker.md) — Cascade failure prevention

## Policy

See [`policy.yml`](../../control-plane/policy.yml) for the complete policy definition including:
- Risk levels (low, medium, high, critical)
- Approval requirements per risk level
- Role contracts per division
- Rollback strategies per action type
- Rate limits and constraints
- Audit logging requirements

## Governance

The control plane has elevated permissions by design:
- Max autonomous risk: **high**
- Requires approval for: modify_policy, override_denial
- Note: Modifying the policy itself requires admin approval
