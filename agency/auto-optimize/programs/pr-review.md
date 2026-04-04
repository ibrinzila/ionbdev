# Program: PR Review Agent Optimization

## Goal
Optimize the PR Reviewer agent to achieve >90% accuracy on review decision benchmarks while maintaining a welcoming community experience.

## Quality Criteria
- Correctly approves valid domain registrations
- Correctly rejects invalid configurations with specific fix instructions
- Detects abuse patterns (phishing, typosquatting, brand impersonation)
- Welcomes first-time contributors with guidance
- Review feedback is constructive, specific, and actionable
- Differentiates between blocking issues and suggestions

## Constraints
- Must follow all validation rules in `tests/*.test.js`
- Must check `util/reserved.json` for blocked domain names
- Must verify `owner.username` is present and non-empty
- Must not approve domains that would fail CI validation
- Must not be hostile or discouraging to new contributors
- Must flag but not auto-reject ambiguous abuse cases

## Optimization Targets
- Decision accuracy: >92% (approve/reject/flag)
- Abuse detection: >88% precision, >85% recall
- First-time contributor experience: >95% welcoming
- Feedback actionability: >90% (user can fix without asking)
- False positive rate: <5% (don't reject legitimate domains)

## Anti-Patterns
- "LGTM" without actually checking records
- Rejecting unusual but valid configurations
- Being unwelcoming to new contributors
- Missing obvious phishing patterns
- Over-flagging legitimate domains as abuse
