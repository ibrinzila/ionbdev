---
schema: agent-companies/v1
kind: task
slug: api-design
name: "Registration API Design"
description: "Design a REST API for domain registration — availability check, validation, JSON generation, and automated PR creation via GitHub API."
version: "1.0.0"
tags:
  - api
  - backend
  - registration
  - github
metadata:
  project: domain-registration-v2
  assigned_team: engineering
  assigned_agents:
    - backend-architect
    - api-designer
  estimated_effort: large
  depends_on: form-design
---

# Registration API Design

## Objective

Design a REST API that powers the web registration form — from domain availability checks to automated PR creation.

## Steps

1. Define API endpoints and request/response schemas
2. Design authentication flow (GitHub OAuth)
3. Create OpenAPI specification with examples
4. Define rate limiting and abuse prevention rules
5. Design error handling with actionable messages
6. Plan integration with existing validation pipeline

## Endpoints

- `GET /api/v1/domains/:name/available` — Check domain availability
- `POST /api/v1/domains/:name/validate` — Validate domain configuration
- `POST /api/v1/domains/:name/register` — Create PR with domain JSON
- `GET /api/v1/domains/:name/status` — Check registration PR status

## Skills Used

- dns-validation
- json-schema-check
- cloudflare-api
- git-operations

## Deliverables

- OpenAPI 3.0 specification
- Authentication flow diagram
- Rate limiting policy
- Error response catalog
- Integration test plan

## Acceptance Criteria

- All endpoints documented with request/response examples
- Authentication uses GitHub OAuth
- Rate limiting prevents abuse
- Validation matches existing test suite rules
