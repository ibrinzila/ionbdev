# The World Model — Autonomous Company Intelligence

> "In practice, Spectre is the beginning of a company world model: a live picture of what is happening inside the company and what needs to happen next."

---

## The Shift

Most AI agent systems are **reactive** — a human types a prompt, an agent responds. That's useful, but it's not how companies actually work.

Real companies are driven by **events**:
- A bug report lands in GitHub Issues
- DNS resolution latency spikes at 3am
- A contributor submits their 50th PR
- A Cloudflare alert fires
- Community sentiment shifts on Discord
- A domain registration looks like phishing
- The CI pipeline fails on main
- A dependency has a critical CVE

These events don't wait for someone to type a prompt. They happen continuously, and the organization's ability to **detect, prioritize, and route** them determines how fast it moves.

The bottleneck is no longer implementation. **The bottleneck is coordination.**

---

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    EVENT SOURCES                          │
│                                                          │
│  GitHub    DNS      Cloudflare   Discord   CI/CD   CVEs  │
│  Issues    Health   Alerts       Messages  Builds  Deps  │
│  PRs       Latency  Analytics    Sentiment Deploys Audit  │
│  Commits   Zones    WAF          Reports   Tests   Logs  │
└──────────────────┬───────────────────────────────────────┘
                   │ raw events
                   ▼
┌──────────────────────────────────────────────────────────┐
│                   EVENT INGESTION                         │
│                                                          │
│  Normalize events into structured signals                │
│  Deduplicate and correlate related events                │
│  Attach context (affected domains, owners, history)      │
│  Tag with source, timestamp, and initial severity        │
└──────────────────┬───────────────────────────────────────┘
                   │ structured signals
                   ▼
┌──────────────────────────────────────────────────────────┐
│                  COMPANY WORLD MODEL                      │
│                                                          │
│  A live picture of what is happening and                 │
│  what needs to happen next.                              │
│                                                          │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐        │
│  │ System     │  │ Community  │  │ Product    │        │
│  │ Health     │  │ Health     │  │ Health     │        │
│  │            │  │            │  │            │        │
│  │ DNS up?    │  │ PRs queue? │  │ Features?  │        │
│  │ CI green?  │  │ Sentiment? │  │ Roadmap?   │        │
│  │ Deps safe? │  │ Growth?    │  │ Feedback?  │        │
│  └────────────┘  └────────────┘  └────────────┘        │
│                                                          │
│  State: What IS happening                                │
│  Intent: What SHOULD happen next                         │
│  Priority: What matters MOST right now                   │
└──────────────────┬───────────────────────────────────────┘
                   │ decisions
                   ▼
┌──────────────────────────────────────────────────────────┐
│                 DECISION ENGINE                           │
│                                                          │
│  Classify → Prioritize → Route → Act or Escalate        │
│                                                          │
│  "DNS latency spike" → P1 → Operations → Auto-investigate│
│  "New CVE in dep"    → P2 → Engineering → Create PR      │
│  "Phishing domain"   → P1 → Security   → Auto-block      │
│  "PR review backlog" → P3 → Community  → Assign reviewers│
│  "Feature request"   → P4 → Product    → Add to backlog  │
└──────────────────┬───────────────────────────────────────┘
                   │ routed actions
                   ▼
┌──────────────────────────────────────────────────────────┐
│                 CONTROL PLANE                             │
│                                                          │
│  Policy Enforcer → Risk Classifier → Rollback Guardian   │
│  (same governance as before — actions still need policy)  │
└──────────────────┬───────────────────────────────────────┘
                   │ authorized actions
                   ▼
┌──────────────────────────────────────────────────────────┐
│                 AGENT DIVISIONS                           │
│                                                          │
│  Engineering · Design · Operations · Community · ...     │
│  (63 specialized agents execute their domain expertise)   │
└──────────────────────────────────────────────────────────┘
```

---

## The Three Health Models

The world model maintains three live health models that inform all autonomous decisions:

### 1. System Health
What the infrastructure is doing right now.

```yaml
system_health:
  signals:
    - dns_resolution_success_rate    # Target: >99.95%
    - dns_propagation_latency        # Target: <60min for 99%
    - ci_pipeline_success_rate       # Target: >98%
    - ci_pipeline_duration           # Target: <3min
    - cloudflare_cache_hit_ratio     # Target: >90%
    - dependency_vulnerability_count # Target: 0 critical
    - api_error_rate                 # Target: <0.1%
    - zone_record_count              # Informational

  thresholds:
    healthy: all_targets_met
    degraded: 1-2_targets_missed
    critical: 3+_targets_missed OR dns_resolution <99%
```

### 2. Community Health
What the contributor community is doing right now.

```yaml
community_health:
  signals:
    - open_pr_count                  # Target: <50
    - pr_review_latency_p50          # Target: <12h
    - pr_review_latency_p95          # Target: <48h
    - first_time_contributor_count   # Trending up
    - contributor_retention_rate     # Target: >70%
    - discord_active_members         # Trending up
    - github_stars_weekly            # Trending up
    - abuse_reports_open             # Target: <5
    - community_sentiment_score      # Target: >0.7 (positive)

  thresholds:
    healthy: pr_backlog_low AND sentiment_positive
    degraded: pr_backlog_growing OR sentiment_declining
    critical: pr_backlog >100 OR abuse_reports >20
```

### 3. Product Health
What the product trajectory looks like right now.

```yaml
product_health:
  signals:
    - daily_registrations            # Trending up
    - registration_completion_rate   # Target: >60%
    - time_to_first_domain_p50      # Target: <30min
    - active_domains_count           # Trending up
    - domain_churn_rate             # Target: <2%/month
    - feature_request_velocity      # Informational
    - bug_report_velocity           # Trending down
    - documentation_coverage        # Target: >90%

  thresholds:
    healthy: growth_positive AND bugs_decreasing
    degraded: growth_flat OR bugs_increasing
    critical: growth_negative OR completion_rate <40%
```

---

## Decision Engine

The decision engine turns signals into actions. It doesn't wait for prompts.

### Decision Types

| Type | Trigger | Example | Autonomy |
|------|---------|---------|----------|
| **Incident** | System health threshold breach | DNS resolution drops below 99% | Auto-investigate, alert humans |
| **Security** | Abuse pattern detected | Phishing domain registered | Auto-block if confidence >0.95, else flag for review |
| **Maintenance** | Dependency CVE published | Critical vulnerability in npm dep | Auto-create PR with fix |
| **Community** | PR backlog growing | >50 open PRs for >24h | Auto-assign reviewers, priority label |
| **Growth** | Registration trend change | Completions drop 20% week-over-week | Alert Product, create investigation task |
| **Coordination** | Cross-team dependency | Engineering blocked on Design review | Auto-escalate, suggest priority swap |

### Decision Flow

```python
class DecisionEngine:
    def process_signal(self, signal: Signal) -> list[Decision]:
        """Turn a signal into zero or more decisions."""
        decisions = []

        # 1. Update world model state
        self.world_model.update(signal)

        # 2. Check if any thresholds are breached
        health_changes = self.world_model.check_thresholds()

        for change in health_changes:
            if change.severity == "critical":
                # Auto-escalate critical issues
                decisions.append(Decision(
                    type="incident",
                    priority="P1",
                    route_to="operations",
                    action="investigate_and_mitigate",
                    autonomy="auto_investigate_human_approve_fix",
                    signal=signal,
                ))
            elif change.severity == "degraded":
                # Create investigation task
                decisions.append(Decision(
                    type="investigation",
                    priority="P2",
                    route_to=self.best_team_for(signal),
                    action="investigate_root_cause",
                    autonomy="auto_investigate_auto_fix_if_low_risk",
                    signal=signal,
                ))

        # 3. Check pattern-based rules
        pattern_decisions = self.pattern_matcher.evaluate(signal)
        decisions.extend(pattern_decisions)

        # 4. All decisions go through control plane
        return [d for d in decisions if self.control_plane.authorize(d)]
```

### Autonomy Levels

Not every decision needs a human. The world model operates at four autonomy levels:

```yaml
autonomy_levels:
  full_auto:
    description: "Execute without human input"
    examples:
      - Label incoming PRs based on changed files
      - Run validation on new domain registrations
      - Deduplicate incoming bug reports
      - Update monitoring dashboards
    risk: low

  auto_investigate_human_approve:
    description: "Research and recommend, human decides"
    examples:
      - Investigate DNS latency spike, propose fix, wait for approval
      - Analyze abuse pattern, recommend block, wait for confirmation
      - Draft PR for dependency update, request review
    risk: medium

  auto_investigate_auto_fix_if_low_risk:
    description: "Research, fix if safe, escalate if not"
    examples:
      - Fix typo in documentation, auto-merge
      - Update minor dependency, auto-merge if tests pass
      - Assign reviewer to stale PR
    risk: medium (with low-risk auto-fix)

  human_required:
    description: "Alert and wait for human decision"
    examples:
      - Production DNS zone changes
      - Policy modifications
      - Banning users
      - Architecture decisions
    risk: high/critical
```

---

## Event Source Monitors

The world model watches these event sources continuously:

### GitHub Monitor
```yaml
github_monitor:
  watches:
    - issues: [opened, labeled, commented, closed]
    - pull_requests: [opened, review_requested, approved, merged, closed]
    - commits: [pushed_to_main]
    - actions: [workflow_run_completed, workflow_run_failed]
    - releases: [published]
    - security: [dependabot_alert, code_scanning_alert]

  patterns:
    pr_backlog_growing:
      condition: open_prs > 50 AND avg_age > 24h
      action: auto_assign_reviewers AND notify_community_team

    ci_repeatedly_failing:
      condition: main_branch_failures > 3 in 1h
      action: create_incident AND notify_engineering

    first_time_contributor:
      condition: pr_author.contributions == 0
      action: add_welcome_comment AND assign_experienced_reviewer
```

### DNS Health Monitor
```yaml
dns_monitor:
  watches:
    - resolution_success_rate: [every 1m]
    - propagation_latency: [every 5m]
    - record_count: [every 1h]
    - dnssec_validation: [every 1h]
    - subdomain_takeover_scan: [every 24h]

  patterns:
    resolution_degraded:
      condition: success_rate < 99.5% for 5m
      action: create_p1_incident AND page_operations

    propagation_slow:
      condition: p95_latency > 2h
      action: investigate_cloudflare_status AND notify_operations

    potential_takeover:
      condition: dangling_cname_detected
      action: create_p1_security_alert AND auto_disable_record
```

### Community Sentiment Monitor
```yaml
community_monitor:
  watches:
    - discord_messages: [help_channel, general, feedback]
    - github_issue_sentiment: [every 1h]
    - social_media_mentions: [every 1h]

  patterns:
    negative_sentiment_spike:
      condition: sentiment_score drops > 20% in 24h
      action: investigate_cause AND alert_community_team

    support_overwhelmed:
      condition: unanswered_questions > 20 AND oldest > 12h
      action: draft_responses AND notify_support_team

    community_milestone:
      condition: github_stars crosses 1000_increment
      action: draft_celebration_post AND notify_marketing
```

---

## The Coordination Problem

Harvey's key insight: **"The bottlenecks are shifting away from implementation and toward review, prioritization, coordination, and operating design."**

The world model addresses this directly:

### Before (Human Coordination)
```
Bug reported → sits in queue → human triages → assigns team →
team investigates → human reviews → human approves → deploys
```
Average time: days to weeks.

### After (World Model Coordination)
```
Bug reported → auto-classified by severity → routed to best team →
investigation started immediately → fix proposed → human reviews critical path only
```
Average time: minutes to hours for P1, hours to days for P3.

### What Gets Automated vs. What Stays Human

| Automated (Machine Coordination) | Human (Judgment & Oversight) |
|----------------------------------|------------------------------|
| Event detection and classification | Architecture decisions |
| Priority scoring and routing | Policy changes |
| PR assignment and labeling | User bans and abuse escalations |
| Dependency updates | Production DNS changes |
| Documentation fixes | Feature prioritization |
| Monitoring and alerting | Community conflict resolution |
| Investigation and root cause analysis | Strategic partnerships |
| Draft responses and fixes | Final approval on high-risk actions |

The goal isn't to remove humans. It's to make human attention **the scarcest resource that gets applied only where judgment matters most**.

---

## Integration with Existing Layers

The world model sits on top of everything we've built:

```
┌─────────────────────────────────────────┐
│ WORLD MODEL (this layer)                │  ← Watches, decides, routes
│ Event sources → Health models →         │
│ Decision engine → Autonomy levels       │
├─────────────────────────────────────────┤
│ AGENT COMPANIES PROTOCOL                │  ← Describes who exists
│ COMPANY.md → TEAM.md → SKILL.md →      │
│ PROJECT.md → TASK.md                    │
├─────────────────────────────────────────┤
│ CONTROL PLANE                           │  ← Governs what's allowed
│ Policy → Risk → Approvals →            │
│ Rollback → Audit → Circuit Breakers    │
├─────────────────────────────────────────┤
│ AGENT DIVISIONS                         │  ← Does the work
│ 63 agents across 13 divisions           │
│ Each with expertise, process, metrics   │
└─────────────────────────────────────────┘
```

**Layer 1: Agents** — specialized expertise (what to do)
**Layer 2: Control Plane** — governance (what's allowed)
**Layer 3: Protocol** — structure (who exists, how work flows)
**Layer 4: World Model** — intelligence (what's happening, what's next)
