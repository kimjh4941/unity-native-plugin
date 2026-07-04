---
name: review-implementation-sample-scene
description: Wrapper for the shared sample scene implementation review workflow in `agent-rules/workflows/review-implementation-sample-scene/workflow.md`. Use when Codex should review sample scene implementation changes against plans, results, and repository rules.
---

# Review Implementation Sample Scene

Read `agent-rules/workflows/review-implementation-sample-scene/workflow.md` before taking action.

## Instructions

1. Treat the workflow file as the single source of truth.
2. Follow the workflow steps in order, including diff selection, interactive inputs, and repository rule loading.
3. Read the referenced design, result, and coding-rule files before forming review findings.
4. If the workflow and another local instruction appear to conflict, surface the conflict and preserve the stricter repository rule.
