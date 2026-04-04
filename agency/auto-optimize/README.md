# Auto-Optimize — The Self-Improving Agency

> Instead of manually tuning agents, a meta-agent automatically optimizes them through benchmarks. Score goes up? Keep the change. Score goes down? Discard it. Repeat forever.

Inspired by [AutoAgent](https://github.com/kevinrgu/autoagent) — a framework for autonomous agent engineering.

---

## The Problem

Hand-tuning 69 agents doesn't scale. As the agency grows:
- How do you know which agents are actually good at their job?
- How do you improve an agent without breaking what already works?
- How do you measure quality objectively, not subjectively?

## The Solution

**Treat agent optimization like automated research.**

```
┌─────────────────────────────────────────────────────┐
│                 OPTIMIZATION LOOP                    │
│                                                      │
│   1. Read program.md (human-written specification)   │
│   2. Read current agent definition                   │
│   3. Run benchmark tasks                             │
│   4. Score results (0.0 → 1.0)                       │
│   5. Meta-agent proposes modifications               │
│   6. Run benchmarks again                            │
│   7. Score improved? → Keep changes                  │
│      Score worse?   → Discard changes                │
│   8. Repeat                                          │
│                                                      │
│   Human writes the spec. Machine optimizes the agent.│
└─────────────────────────────────────────────────────┘
```

---

## Architecture

```
auto-optimize/
├── programs/              # Human-written specifications per agent
│   ├── dns-validation.md  # "What a great DNS validator looks like"
│   ├── pr-review.md       # "What a great PR reviewer looks like"
│   ├── abuse-detection.md # "What a great abuse detector looks like"
│   └── ...
├── benchmarks/            # Objective evaluation tasks
│   ├── dns-validation/    # Test cases with expected outcomes
│   ├── pr-review/         # Sample PRs with correct review decisions
│   ├── abuse-detection/   # Domains with known abuse/legitimate labels
│   ├── incident-response/ # Incident scenarios with optimal responses
│   └── coordination/      # Cross-team scenarios with ideal routing
├── results/               # Score history and optimization logs
└── README.md              # This file
```

### The Three Files

| File | Who Writes It | What It Does |
|------|---------------|-------------|
| `programs/*.md` | Human | Specifies what a great agent looks like — goals, constraints, quality bar |
| `benchmarks/*/` | Human + Machine | Test cases with inputs and expected outputs — the objective truth |
| Agent files | Machine (meta-agent) | The actual agent definitions — optimized by the loop |

**The key insight from AutoAgent**: humans program the meta-agent through `program.md`, not the agent directly. You describe *what* great looks like. The machine figures out *how*.

---

## How It Works

### Step 1: Human Writes the Program

```markdown
# programs/pr-review.md

## Goal
Optimize the PR Reviewer agent for the is-a.dev project.

## Quality Criteria
- Correctly identifies valid domain registrations (precision >95%)
- Correctly rejects invalid registrations (recall >90%)
- Provides specific, actionable feedback for rejected PRs
- Welcomes first-time contributors with appropriate guidance
- Review turnaround time suggestion is reasonable

## Constraints
- Must follow existing validation rules in tests/
- Must not approve domains on the reserved list
- Must detect CNAME conflicts with other record types
- Must verify owner.username is present and non-empty

## Anti-Patterns to Avoid
- Generic "looks good" approvals without checking records
- Rejecting valid but unusual configurations (e.g., SRV records)
- Being unwelcoming to first-time contributors
- Missing abuse indicators in domain names
```

### Step 2: Machine Runs Benchmarks

Each benchmark is a test case with:
- **Input**: A simulated PR or domain file
- **Expected output**: The correct review decision + reasoning
- **Scoring**: 0.0 (completely wrong) to 1.0 (perfect)

### Step 3: Meta-Agent Optimizes

The meta-agent reads the program, runs benchmarks, analyzes failures, and proposes modifications to the agent file. If the modification improves the total score, it's kept. If not, it's discarded.

```python
class OptimizationLoop:
    def run(self, agent_path: str, program_path: str, benchmark_dir: str):
        # Load current agent and program spec
        agent = load_agent(agent_path)
        program = load_program(program_path)
        
        # Baseline score
        baseline_score = self.evaluate(agent, benchmark_dir)
        print(f"Baseline score: {baseline_score:.3f}")
        
        for iteration in range(max_iterations):
            # Meta-agent proposes modification
            modified_agent = self.meta_agent.propose_improvement(
                agent=agent,
                program=program,
                failures=self.get_failures(agent, benchmark_dir),
            )
            
            # Evaluate modified agent
            new_score = self.evaluate(modified_agent, benchmark_dir)
            
            if new_score > baseline_score:
                print(f"  Iteration {iteration}: {baseline_score:.3f} → {new_score:.3f} ✓ KEEP")
                agent = modified_agent
                baseline_score = new_score
                save_agent(agent, agent_path)
            else:
                print(f"  Iteration {iteration}: {baseline_score:.3f} → {new_score:.3f} ✗ DISCARD")
        
        return agent, baseline_score
```

---

## Benchmark Design

### DNS Validation Benchmarks

```yaml
# benchmarks/dns-validation/valid-a-record.yml
name: "Valid A record with public IPv4"
input:
  filename: "mysite.json"
  content:
    owner: { username: "testuser" }
    record: { A: ["203.0.113.1"] }
expected:
  result: pass
  errors: []
scoring:
  correct_result: 0.5
  correct_reasoning: 0.3
  actionable_message: 0.2

# benchmarks/dns-validation/private-ip-rejection.yml
name: "Reject private IPv4 address"
input:
  filename: "mysite.json"
  content:
    owner: { username: "testuser" }
    record: { A: ["192.168.1.1"] }
expected:
  result: fail
  errors: ["Private IP not allowed"]
scoring:
  correct_result: 0.5
  identifies_private_ip: 0.3
  suggests_fix: 0.2

# benchmarks/dns-validation/cname-conflict.yml
name: "Reject CNAME with coexisting A record"
input:
  filename: "mysite.json"
  content:
    owner: { username: "testuser" }
    record: { CNAME: "example.com", A: ["1.2.3.4"] }
expected:
  result: fail
  errors: ["CNAME cannot coexist with other record types"]
scoring:
  correct_result: 0.5
  correct_error_message: 0.3
  explains_why: 0.2
```

### PR Review Benchmarks

```yaml
# benchmarks/pr-review/first-time-valid.yml
name: "First-time contributor with valid domain"
input:
  pr_author: "newdev123"
  author_contributions: 0
  files_changed:
    - path: "domains/newdev.json"
      content:
        owner: { username: "newdev123" }
        record: { CNAME: "newdev123.github.io" }
expected:
  decision: approve
  includes_welcome: true
  validation_passed: true
scoring:
  correct_decision: 0.4
  welcome_message: 0.2
  validation_thoroughness: 0.2
  actionable_feedback: 0.2

# benchmarks/pr-review/phishing-domain.yml
name: "Phishing domain impersonating PayPal"
input:
  pr_author: "randomuser999"
  author_contributions: 0
  files_changed:
    - path: "domains/paypa1-login.json"
      content:
        owner: { username: "randomuser999" }
        record: { CNAME: "suspicious-host.workers.dev" }
expected:
  decision: reject
  reason: phishing_indicator
  flags: [brand_impersonation, suspicious_target, new_account]
scoring:
  correct_decision: 0.4
  identifies_phishing: 0.3
  evidence_quality: 0.2
  appropriate_tone: 0.1
```

### Abuse Detection Benchmarks

```yaml
# benchmarks/abuse-detection/typosquat.yml
name: "Typosquatting of Google"
input:
  domain: "go0gle"
  record: { CNAME: "phish-host.pages.dev" }
  owner: { username: "newaccount2026" }
expected:
  classification: malicious
  confidence: ">0.85"
  indicators: [brand_typosquat, suspicious_cname, new_account]
scoring:
  correct_classification: 0.4
  confidence_accuracy: 0.2
  indicator_identification: 0.3
  false_positive_avoidance: 0.1

# benchmarks/abuse-detection/legitimate-similar.yml
name: "Legitimate domain that looks like a brand"
input:
  domain: "googler-portfolio"
  record: { CNAME: "googler.github.io" }
  owner: { username: "ex-googler", contributions: 15 }
expected:
  classification: legitimate
  confidence: ">0.70"
  reasoning: "Personal portfolio, established contributor"
scoring:
  correct_classification: 0.5
  avoids_false_positive: 0.3
  reasoning_quality: 0.2
```

---

## Scoreboard

Track optimization progress over time:

```markdown
## Agent Scores — 2026-04-04

| Agent | Baseline | Current | Δ | Iterations | Status |
|-------|----------|---------|---|-----------|--------|
| DNS Validator | 0.72 | — | — | 0 | Not started |
| PR Reviewer | 0.68 | — | — | 0 | Not started |
| Abuse Detector | 0.61 | — | — | 0 | Not started |
| Incident Commander | 0.75 | — | — | 0 | Not started |
| Coordination Layer | 0.55 | — | — | 0 | Not started |
```

---

## Integration with the Agency Stack

```
┌─────────────────────────────────────────────┐
│  AUTO-OPTIMIZE (Layer 5)                    │  ← NEW: Self-improvement
│  Programs → Benchmarks → Meta-Agent →       │
│  Score → Keep or Discard → Repeat           │
├─────────────────────────────────────────────┤
│  WORLD MODEL (Layer 4)                      │  ← Watches, decides, acts
│  Monitors → Decision Engine → Coordination  │
├─────────────────────────────────────────────┤
│  AGENT COMPANIES PROTOCOL (Layer 3)         │  ← Portable structure
│  COMPANY.md → TEAM.md → SKILL.md            │
├─────────────────────────────────────────────┤
│  CONTROL PLANE (Layer 2)                    │  ← Governance
│  Policy → Risk → Rollback → Audit           │
├─────────────────────────────────────────────┤
│  AGENT DIVISIONS (Layer 1)                  │  ← Specialized expertise
│  69 agents across 14 divisions              │
└─────────────────────────────────────────────┘
```

The auto-optimize layer closes the loop: agents do work → benchmarks measure quality → meta-agent improves agents → agents do better work.

**Humans write specs. Machines optimize execution. Benchmarks keep everyone honest.**

---

## Philosophy

From [AutoAgent](https://github.com/kevinrgu/autoagent):

> "You program a Markdown file that guides an AI meta-agent to automatically build, test, and iterate on agent implementations."

This is **indirect control**. You don't hand-tune prompts. You define what great looks like, build objective benchmarks, and let the optimization loop find the best implementation.

The metric is total score across all test suites — a hill-climbing approach. Simple, objective, automated.
