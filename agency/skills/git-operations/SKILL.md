---
schema: agent-companies/v1
kind: skill
slug: git-operations
name: "Safe Git Workflow Operations"
description: "When you need to perform git operations — branching, committing, PR creation, merging, and reverting with safety checks and rollback awareness."
version: "1.0.0"
tags:
  - git
  - version-control
  - workflow
  - safety
---

# Safe Git Workflow Operations

Perform git operations with built-in safety checks and rollback awareness.

## When to Use

- Creating branches for feature work
- Committing and pushing changes
- Creating and merging pull requests
- Reverting changes when something goes wrong

## Process

1. Verify current branch and clean working state
2. Execute the git operation with safety checks:
   - **Branch**: Create from latest main, use naming conventions
   - **Commit**: Meaningful messages, no secrets, no large binaries
   - **Push**: Use `-u` flag, never force-push to main
   - **Merge**: Require CI pass and approval, squash-and-merge
   - **Revert**: Create revert commit, never `reset --hard` on shared branches
3. Verify the operation succeeded
4. Record the operation for audit trail

## Constraints

- Never force-push to main/master
- Never commit files matching `.env`, `credentials.*`, `*.key`
- Always verify CI passes before merge
- Keep commits atomic and meaningful

## Rollback

- **Commit**: `git revert <sha>`
- **Merge**: `git revert -m 1 <merge-sha>` or revert PR
- **Push**: Revert commit + push
