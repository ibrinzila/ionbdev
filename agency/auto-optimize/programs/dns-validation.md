# Program: DNS Validation Agent Optimization

## Goal
Optimize the DNS Specialist and JSON Schema Validator agents to achieve >95% accuracy on all DNS validation benchmarks.

## Quality Criteria
- Correctly validates all RFC-compliant record types (A, AAAA, CNAME, MX, NS, TXT, CAA, DS, SRV, URL)
- Rejects private/reserved IP ranges (except 192.0.2.1 for proxied records)
- Detects CNAME conflicts with other record types
- Validates FQDN format (1-253 chars, valid characters)
- Identifies malformed IPv6 addresses (link-local, multicast, documentation ranges)
- Error messages are specific enough to fix the issue without documentation

## Constraints
- Must match the validation rules in `tests/records.test.js`
- Must not reject valid but uncommon configurations
- Must not introduce false positives for edge cases in `util/excepted.json`
- Processing time must stay under 100ms per domain file

## Optimization Targets
- Precision: >97% (don't reject valid records)
- Recall: >95% (don't miss invalid records)
- Error message quality: >90% (actionable and specific)
- Edge case handling: >85% (uncommon but valid configurations)

## Anti-Patterns
- Rejecting valid SRV or DS records because they're uncommon
- Missing IPv6 validation edge cases
- Generic error messages like "invalid record"
- Allowing CNAME with NS records (invalid per RFC)
