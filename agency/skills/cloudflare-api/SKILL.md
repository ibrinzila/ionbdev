---
schema: agent-companies/v1
kind: skill
slug: cloudflare-api
name: "Cloudflare DNS API Operations"
description: "When you need to interact with the Cloudflare API — manage DNS records, configure proxy settings, check zone status, and monitor analytics."
version: "1.0.0"
tags:
  - cloudflare
  - dns
  - api
  - cdn
---

# Cloudflare DNS API Operations

Interact with the Cloudflare API for DNS management, CDN configuration, and zone operations.

## When to Use

- Publishing DNS records to Cloudflare
- Querying zone status and record details
- Configuring proxy and caching settings
- Monitoring DNS analytics and performance

## Process

1. Authenticate using API token (Bearer token)
2. Identify the target zone ID
3. Execute the API operation:
   - **List records**: `GET /zones/{zone_id}/dns_records`
   - **Create record**: `POST /zones/{zone_id}/dns_records`
   - **Update record**: `PUT /zones/{zone_id}/dns_records/{id}`
   - **Delete record**: `DELETE /zones/{zone_id}/dns_records/{id}`
4. Verify the response and handle errors
5. Log the operation for audit trail

## Constraints

- API token must never be committed to code
- Rate limit: 1200 requests per 5 minutes
- Proxied records have automatic TTL (TTL=1)
- CNAME flattening is automatic at the zone apex

## Inputs

- Zone ID
- API token (from environment)
- Record data (type, name, content, proxied, ttl)

## Outputs

- API response with record details
- Error details if operation failed
- Confirmation of record state
