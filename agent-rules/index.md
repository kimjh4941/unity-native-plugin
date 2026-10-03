# AI Agent Shared Rules

This file is the shared entry point for all AI agents used in this repository.
All implementation rules are managed in this folder.

## Index

- Common implementation policy (Bridge pattern / sample scenes / TDD): ./coding-rules/common.md
- C# coding rules (Unity6): ./coding-rules/csharp.md
- Test strategy (test layers / per-platform tooling): ./coding-rules/testing.md
- Agent tooling notes on Windows (escaping, line endings, Japanese .py files, searches): ./tooling-notes.md

## Artifacts

Designs, results, reviews and the issues carved out as separate tasks.
`artifact/` is split by scope: `<os>/<feature>/` holds workflow output for one
OS feature, `topics/<topic>/` holds issues that cut across features.

- Artifact map (成果物の置き場と、未対応として切り出した課題の一覧): ../artifact/README.md

**Read the issue list there before starting work in an area.** Four issues are
recorded as deliberately deferred, and two of them are only half applied.

## Workflows

Canonical workflow definitions shared across all agents (Copilot, Claude, Codex).
Agent-specific wrappers in `.github/` reference these files.

- Design feature (実装計画作成): ./workflows/design-feature/workflow.md
- Implement feature (実装・テスト・確認): ./workflows/implement-feature/workflow.md
- Design sample scene (サンプルシーン計画作成): ./workflows/design-sample-scene/workflow.md
- Implement sample scene (サンプルシーン実装): ./workflows/implement-sample-scene/workflow.md
- Review document (実装計画書レビュー): ./workflows/review-document/workflow.md
- Review implementation feature (実装レビュー): ./workflows/review-implementation-feature/workflow.md
- Review implementation sample scene (サンプルシーンレビュー): ./workflows/review-implementation-sample-scene/workflow.md
- Commit message (コミットメッセージ生成): ./workflows/commit-msg/workflow.md
- Write manual (マニュアル生成・公開): ./workflows/write-manual/workflow.md
- Verify manual (マニュアル整合検査): ./workflows/verify-manual/workflow.md
- Release (リリース PR・タグ・GitHub Release): ./workflows/release/workflow.md

## Common policy

- Write comment text in English.
- Write user-facing message text in English.
- When adding rules, update this index and place details in each rule file.

## Working with the user

- Reply to the user in Japanese, long reports included. Commit messages, code, identifiers and test names stay in English.
- When the user has to decide, name the option you recommend and why in one line. Do not present a neutral list of choices.
- Do not offer to stop or pause at a milestone. Finish the step, report the result, and go on to the next one; the user says when to stop. Still ask before anything hard to reverse or outward-facing (commits, pushes, PRs, releases, messages to others).
- Choose the number of subagents or reviewers yourself from the work (distinct viewpoints, not volume), and state the choice in one line.
- Write findings, decisions and remaining work into `artifact/` when they happen, not at release time. An agent's own memory and its session transcripts are only a backup: they stay on one machine, and transcripts are deleted after a retention period.
