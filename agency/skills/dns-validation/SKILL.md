---
schema: agent-companies/v1
kind: skill
slug: dns-validation
name: "DNS Record Validation"
description: "When you need to validate DNS records against RFC standards — checks A, AAAA, CNAME, MX, NS, TXT, CAA, DS, SRV, and URL record types for correctness, conflicts, and security issues."
version: "1.0.0"
tags:
  - dns
  - validation
  - rfc
  - security
---

# DNS Record Validation

Validate DNS records for correctness, RFC compliance, and security.

## When to Use

- Validating domain registration JSON files
- Checking DNS record configurations before publishing
- Detecting conflicting record types (e.g., CNAME with A)
- Identifying private/reserved IP addresses
- Verifying FQDN formatting

## Process

1. Parse the domain JSON file
2. Verify at least one record type exists
3. Validate each record type against its RFC specification:
   - **A**: Valid public IPv4 addresses (no private ranges except 192.0.2.1 for proxied)
   - **AAAA**: Valid IPv6 addresses (no link-local, multicast, documentation ranges)
   - **CNAME**: Valid hostname, cannot coexist with other record types
   - **MX**: Valid hostname array
   - **NS**: Valid nameserver hostnames
   - **TXT**: String or array of strings
   - **CAA**: Objects with tag and value
   - **DS**: Objects with key_tag, algorithm, digest_type, digest
   - **SRV**: Objects with priority, weight, port, target
   - **URL**: Valid URL (becomes A record 192.0.2.1 with proxying)
4. Check for conflicting record combinations
5. Validate hostname format (1-253 chars, valid characters)

## Inputs

- Domain JSON file content
- Domain filename (for naming rule validation)

## Outputs

- Validation result (pass/fail)
- List of errors with specific field and rule violated
- Suggestions for fixing each error

## Example

```javascript
// Validate A records
if (record.A) {
  for (const ip of record.A) {
    if (!isValidIPv4(ip)) errors.push(`Invalid IPv4: ${ip}`);
    if (isPrivateIP(ip) && ip !== '192.0.2.1') errors.push(`Private IP not allowed: ${ip}`);
  }
}

// Check CNAME conflicts
if (record.CNAME && Object.keys(record).length > 1) {
  errors.push('CNAME cannot coexist with other record types');
}
```
