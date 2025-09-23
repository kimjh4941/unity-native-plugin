# Native Toolkit (Unity 6)

クロスプラットフォーム対応のネイティブ機能を提供します。
バージョン: 1.0.0

# 機能一覧

## Android

- ダイアログ機能
  - 基本ダイアログ
  - 確認ダイアログ
  - シングル選択ダイアログ
  - マルチ選択ダイアログ
  - 入力ダイアログ
  - ログインダイアログ

## iOS

- ダイアログ機能
  - 基本ダイアログ
  - 確認ダイアログ
  - ディストラクティブなダイアログ
  - アクションシート
  - 入力ダイアログ
  - ログインダイアログ

## Windows

- ダイアログ機能
  - 基本ダイアログ
  - ファイル選択ダイアログ
  - 複数ファイル選択ダイアログ
  - フォルダ選択ダイアログ
  - 複数フォルダ選択ダイアログ
  - ファイル保存ダイアログ

## Mac

- ダイアログ機能

  - 基本ダイアログ
  - ファイル選択ダイアログ
  - 複数ファイル選択ダイアログ
  - フォルダ選択ダイアログ
  - 複数フォルダ選択ダイアログ
  - ファイル保存ダイアログ

## 追加予定機能

- クリップボード連携
- 通知
- シェア機能

# はじめに

## インストール

- Unity6 を起動します。
- Windows - Package Manager を選択します。
- Unity Package Manager → Native Toolkit パッケージを Git URL から追加します。
- 必要条件:
  - Unity 6 以降
  - 依存パッケージ: Localization, Addressables, Input System

## サンプル

-

Package Manager → Samples からインポート

内容:

- NativeToolkitExampleScene.unity
- ダイアログを最小構成で扱うコントローラー群

## エディタウィンドウ

- Tools → Native Toolkit → Example

# API 概要

## ダイアログマネージャ

- AndroidDialogManager
- IosDialogManager
- MacDialogManager
- WindowsDialogManager

### 共通パターン

- スレッド: コールバックはメインスレッド外で届く場合があります。必要に応じてメインスレッドへディスパッチしてください。
- メモリ/Interop: 文字列はマネージ側へコピーされます。各プラットフォームのガイダンスに従ってネイティブリソースを破棄してください。

### AndroidDialogManager（例）

```csharp
AndroidDialogManager.Instance.ShowDialog(
  title: "Confirm",
  message: "Proceed?",
  buttonText: "OK"
);
```

詳細な引数やコールバックの契約はソース内の XML ドキュメントを参照してください。

# トラブルシューティング

## ローカリゼーションが空になる

- SelectedLocale が設定され、`InitializationOperation.IsDone` が完了していることを確認してください。
- テーブルにキーが存在するか、テーブル名が正しいかを確認してください。

## エディタスクリプトがコンパイルされない

- Editor/ 配下に配置し、`NativeToolkit.Editor` asmdef に含まれていることを確認してください。
- asmdef の依存関係を確認: Unity.Localization, Unity.Localization.Editor, Unity.Addressables, Unity.ResourceManager。

## Android/iOS のビルドエラー

- ネイティブ Plugins のインポート設定（プラットフォームフィルタ）を確認。
- Addressables のリソース GUID/レイアウトを変更した場合はビルドをクリーンしてください。

---

English version: index.md
