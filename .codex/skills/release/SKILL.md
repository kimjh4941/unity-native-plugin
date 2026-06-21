---
name: release
description: Wrapper for the shared release workflow in `agent-rules/workflows/release/workflow.md`. Use when Codex should execute this project's standard release checklist, release-note generation, and publishing process.
---

# Release

Read `agent-rules/workflows/release/workflow.md` before taking action.

## Instructions

1. Treat the workflow file as the single source of truth.
2. Follow the workflow steps in order, including the required interactive confirmations and pre-release checks.
3. Read any repository files referenced by the workflow before changing versions, tags, or release artifacts.
4. If the workflow and another local instruction appear to conflict, surface the conflict and preserve the stricter repository rule.
