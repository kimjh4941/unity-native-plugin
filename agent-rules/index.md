# AI Agent Shared Rules

This file is the shared entry point for all AI agents used in this repository.
All implementation rules are managed in this folder.

## Index

- Common implementation policy (Bridge pattern / sample scenes / TDD): ./coding-rules/common.md
- C# coding rules (Unity6): ./coding-rules/csharp.md
- Test strategy (test layers / per-platform tooling): ./coding-rules/testing.md

## Artifacts

Designs, results, reviews and the issues carved out as separate tasks.
`artifact/` is split by scope: `features/<feature>/` holds workflow output for one
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
