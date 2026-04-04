# Program: Abuse Detection Agent Optimization

## Goal
Optimize the Abuse Prevention Specialist to achieve >90% precision and >85% recall on abuse detection benchmarks.

## Quality Criteria
- Detects phishing domains targeting popular brands
- Identifies typosquatting and homoglyph attacks
- Flags suspicious DNS configurations (known malicious hosts)
- Considers account age and contribution history
- Distinguishes between fan projects and impersonation
- Provides evidence for every flag raised

## Constraints
- Must not flag legitimate developer portfolios mentioning employer brands
- Must not flag established contributors with high contribution counts
- Must produce structured evidence (indicators matched, confidence score)
- Must handle internationalized domain names correctly
- False positive rate must stay below 5%

## Optimization Targets
- Phishing precision: >95% (when we say it's phishing, it is)
- Phishing recall: >85% (catch most phishing attempts)
- Typosquatting detection: >90% accuracy
- False positive rate: <5% on legitimate domains
- Evidence quality: >85% (clear, specific, verifiable indicators)

## Anti-Patterns
- Flagging "google-fan-page" from a 2-year-old account as phishing
- Missing obvious letter substitutions (paypa1, g00gle)
- Not considering CNAME targets in the analysis
- Providing vague "looks suspicious" without specific indicators
