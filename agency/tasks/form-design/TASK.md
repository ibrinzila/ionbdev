---
schema: agent-companies/v1
kind: task
slug: form-design
name: "Registration Form UX Design"
description: "Design a web-based domain registration form with real-time validation, DNS record guidance, and accessibility compliance."
version: "1.0.0"
tags:
  - design
  - form
  - ux
  - accessibility
metadata:
  project: domain-registration-v2
  assigned_team: design
  assigned_agents:
    - ui-designer
    - ux-architect
  estimated_effort: large
  depends_on: user-research
---

# Registration Form UX Design

## Objective

Design an intuitive, accessible registration form that guides users from "I want a domain" to "PR submitted" in under 5 minutes.

## Steps

1. Review UX research findings and user personas
2. Design information architecture for the form flow
3. Create wireframes for each step (domain search → record config → review → submit)
4. Design high-fidelity mockups with design system tokens
5. Prototype interactive form for usability testing
6. Conduct accessibility audit (WCAG 2.1 AA)

## Skills Used

- dns-validation (for understanding record types to present)
- json-schema-check (for understanding validation rules)

## Deliverables

- Wireframes for all form steps
- High-fidelity mockups (light and dark mode)
- Interactive prototype
- Accessibility compliance report
- Component specifications for engineering

## Acceptance Criteria

- Form flow is completable in under 5 minutes
- WCAG 2.1 AA compliant
- Works on mobile and desktop
- Error messages are clear and actionable
