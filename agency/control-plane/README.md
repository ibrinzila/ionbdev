# Control Plane — The Missing Layer

> Vibe coding can ship a demo in a day. It can also ship an unapproved production action in a minute. If your agent can execute without explicit policy, approval, and rollback gates, you don't have autonomy. **You have an unmanaged blast radius.**

Models should suggest. **The control plane should decide.**

---

## Why a Control Plane?

Most teams build agent stacks in this order:

1. Prompt
2. Tool call
3. Ship

**Missing layer: control plane.**

Without a control plane, you can't reliably answer:
- **Who** approved this action?
- **What policy** allowed it?
- **What rollback path** existed?

No answer = No production readiness.

---

## What the Control Plane Does

The control plane is where you define and enforce:

- **Role contracts** — what each division can and cannot do
- **Risk thresholds** — how actions are classified by impact
- **Action permissions** — what requires approval vs. auto-executes
- **Approval requirements** — human gates for high-risk actions
- **Rollback policy** — every write has an undo
- **Audit logs** — every action is recorded, forever

---

## The Stack That Works

```
┌─────────────────────────────────────────┐
│           Agent Divisions               │
│  Engineering · Design · Operations ...  │
│         (58 specialized agents)         │
└──────────────────┬──────────────────────┘
                   │ action request
                   ▼
┌─────────────────────────────────────────┐
│          Action Extraction Layer        │
│                                         │
│  1. Receive recommendation + context    │
│  2. Classify risk (Risk Classifier)     │
│  3. Enforce policy (Policy Enforcer)    │
│  4. Check circuit breakers              │
│  5. Verify rollback path exists         │
│  6. Execute or block                    │
│  7. Log everything (Audit Logger)       │
└──────────────────┬──────────────────────┘
                   │ approved action
                   ▼
┌─────────────────────────────────────────┐
│            Execution Layer              │
│                                         │
│  DNS publish · PR merge · Deploy · ...  │
│                                         │
│  Monitored by: Circuit Breaker          │
│  Protected by: Rollback Guardian        │
└─────────────────────────────────────────┘
```

---

## Control Plane Agents (5)

| Agent | Role | Responsibility |
|-------|------|----------------|
| 🚦 [Policy Enforcer](control-plane-policy-enforcer.md) | Gatekeeper | Authorizes or denies every action against policy |
| 📜 [Audit Logger](control-plane-audit-logger.md) | Accountability | Records every action with full context |
| ⏪ [Rollback Guardian](control-plane-rollback-guardian.md) | Safety net | Ensures rollback paths exist, executes rollbacks |
| ⚠️ [Risk Classifier](control-plane-risk-classifier.md) | Risk engine | Classifies action risk based on context and blast radius |
| 🔴 [Circuit Breaker](control-plane-circuit-breaker.md) | Emergency stop | Halts operations when failure rates spike |

---

## Policy File

The [`policy.yml`](policy.yml) file defines:

### Risk Levels
```yaml
risk_levels:
  low: [read, search, validate, summarize]
  medium: [edit, create_pr, update_docs, run_tests]
  high: [merge_pr, publish_dns, deploy, send_notification]
  critical: [delete_zone, bulk_dns_update, rotate_keys]
```

### Approval Requirements
```yaml
approvals:
  low: auto_execute
  medium: auto_if_tests_green
  high: human_required
  critical: admin_required (2 approvals, dry run mandatory)
```

### Role Contracts
Each division has a maximum autonomous risk level:
```yaml
role_contracts:
  engineering: max_autonomous_risk: medium
  operations: max_autonomous_risk: medium (+ DNS publish)
  design: max_autonomous_risk: low
  community: max_autonomous_risk: medium (+ merge PRs)
  ...
```

### Rollback Strategies
Every write action has a defined rollback:
```yaml
rollback:
  edit_file: git_revert (auto on test failure, <5m)
  publish_dns: dnscontrol_revert (auto on health check fail, <5m)
  deploy_production: blue_green_switch (<2m)
  bulk_dns_update: snapshot_restore (<15m)
```

---

## Action Extraction Layer

The action extraction layer is the pipeline that every agent action passes through:

```
Input:  recommendation + confidence + action request + risk map
  │
  ├─ 1. CLASSIFY risk level (Risk Classifier)
  │     └─ Consider: action type, blast radius, temporal factors, agent confidence
  │
  ├─ 2. ENFORCE policy (Policy Enforcer)
  │     └─ Check: role contracts, approval requirements, constraints, rate limits
  │
  ├─ 3. CHECK circuit breakers
  │     └─ If circuit open: block and notify
  │
  ├─ 4. VERIFY rollback path (Rollback Guardian)
  │     └─ Create snapshot if required
  │
  ├─ 5. EXECUTE or BLOCK
  │     └─ Approved: execute with monitoring
  │     └─ Denied: log reason, notify agent, suggest alternative
  │
  └─ 6. LOG everything (Audit Logger)
        └─ Approved action record OR denied-action record

Output: approved action record or denied-action record
```

### Guardrails
- Human gates for all high-risk actions
- Rollback required for all write operations
- Dry run required for DNS publishing and deployments
- Rate limits on sensitive operations
- Unknown actions default to high risk

---

## Failure Modes This Prevents

| Failure Mode | Without Control Plane | With Control Plane |
|-------------|----------------------|-------------------|
| "Smart but reckless" agent | Ships to production unchecked | Blocked by policy, escalated to human |
| Accidental privilege escalation | Agent publishes DNS without authorization | Role contract blocks unauthorized divisions |
| No-audit deployments | No record of who did what | Every action logged with full context |
| Irreversible side effects | DNS zone deleted, no recovery | Rollback Guardian blocks actions without rollback path |
| Cascade failures | One bad publish breaks everything | Circuit breaker halts after 3 failures |

---

## Quick Start

### 1. Review the policy
```bash
cat agency/control-plane/policy.yml
```

### 2. Understand role contracts
Each division has explicit permissions. Check what your agents can do autonomously.

### 3. Activate control plane agents
Reference control plane agents alongside your task agents:
```
@policy-enforcer @dns-specialist Publish DNS records for the latest merged PRs
```

The Policy Enforcer will classify the risk, verify the DNS Specialist has permission, and ensure a rollback path exists before allowing execution.

---

## Philosophy

> Put policy before prompts. It feels slower on day 1. It is much faster on day 30. Because you stop paying chaos tax.

**Speed without governance is theater. Speed with governance is production.**
