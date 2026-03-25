---
schema: agent-companies/v1
kind: skill
slug: json-schema-check
name: "Domain JSON Schema Validation"
description: "When you need to validate domain registration JSON files — checks format, required fields, naming rules, reserved domains, and schema compliance."
version: "1.0.0"
tags:
  - json
  - schema
  - validation
  - domains
---

# Domain JSON Schema Validation

Validate domain registration JSON files for schema compliance and naming rules.

## When to Use

- Reviewing domain registration PRs
- Validating new domain files before merge
- Checking JSON format and required fields
- Enforcing naming conventions and reserved domain rules

## Process

1. Verify valid JSON format (no syntax errors, no duplicate keys)
2. Check filename rules:
   - Lowercase only
   - No `.is-a.dev` in filename
   - Valid FQDN characters (1-253 chars)
   - Not in reserved domains list (`util/reserved.json`)
3. Validate required fields:
   - `owner` object must exist
   - `owner.username` string must exist and be non-empty
4. Validate optional fields:
   - `owner.email` must be valid email format if present
   - `proxied` must be boolean if present
   - `redirect_config` must be object if present
5. Validate `record` object has at least one valid record type
6. Check nested subdomain rules (parent must exist, same owner)

## Inputs

- JSON file content
- Filename
- Reserved domains list
- Excepted domains list

## Outputs

- Validation result (pass/fail)
- List of errors with field path and violation
- Actionable fix suggestions
