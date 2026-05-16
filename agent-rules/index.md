# AI Agent Shared Rules

This file is the shared entry point for all AI agents used in this repository.
All implementation rules are managed in this folder.

## Index

- Common implementation policy (Bridge pattern / TDD): ./coding-rules/common.md
- C# coding rules (Unity6): ./coding-rules/csharp.md

## Workflows

Canonical workflow definitions shared across all agents (Copilot, Claude, Codex).
Agent-specific wrappers in `.github/` reference these files.

- Design implementation (実装計画作成): ./workflows/design-implementation/workflow.md
- Implement feature (実装・テスト・確認): ./workflows/implement-feature/workflow.md
- Design sample scene (サンプルシーン計画作成): ./workflows/design-sample-scene/workflow.md
- Implement sample scene (サンプルシーン実装): ./workflows/implement-sample-scene/workflow.md
- Review document (実装計画書レビュー): ./workflows/review-document/workflow.md
- Review implementation (実装レビュー): ./workflows/review-implementation/workflow.md
- Review sample scene (サンプルシーンレビュー): ./workflows/review-sample-scene/workflow.md
- Commit message (コミットメッセージ生成): ./workflows/commit-msg/workflow.md

## Common policy

- Write comment text in English.
- Write user-facing message text in English.
- When adding rules, update this index and place details in each rule file.
