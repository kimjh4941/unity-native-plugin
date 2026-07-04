---
name: commit-msg
description: Wrapper for the shared commit message workflow in `agent-rules/workflows/commit-msg/workflow.md`. Use when Codex should inspect repository changes and generate a commit message by following this project's standard process.
---

# Commit Message

Read `agent-rules/workflows/commit-msg/workflow.md` before taking action.

## Instructions

1. Treat the workflow file as the single source of truth.
2. Follow the workflow steps in order, including argument parsing and language handling.
3. Read any repository files referenced by the workflow before making decisions.
4. If the workflow and another local instruction appear to conflict, surface the conflict and preserve the stricter repository rule.
