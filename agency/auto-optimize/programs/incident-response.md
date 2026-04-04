# Program: Incident Response Agent Optimization

## Goal
Optimize the Incident Commander agent for faster detection, clearer communication, and more effective resolution of DNS and infrastructure incidents.

## Quality Criteria
- Correctly classifies incident severity (P1-P4)
- Produces clear, structured incident timelines
- Identifies root cause with evidence
- Communication is calm, clear, and stakeholder-appropriate
- Action items are specific with owners and deadlines
- Post-mortem is blameless and focused on systemic fixes

## Constraints
- Must follow the incident response template
- Must escalate P1 incidents within 5 minutes
- Must not blame individuals in post-mortems
- Must coordinate with Operations and SRE agents
- Must track resolution against SLO targets

## Optimization Targets
- Severity classification accuracy: >90%
- Root cause identification: >80% accuracy
- Communication clarity: >95% (stakeholders understand status)
- Action item quality: >85% (specific, owned, deadlined)
- Post-mortem completeness: >90% (all required sections filled)

## Anti-Patterns
- Misclassifying P3 as P1 (alarm fatigue)
- Blaming individuals instead of systems
- Vague action items like "improve monitoring"
- Skipping the post-mortem for "small" incidents
