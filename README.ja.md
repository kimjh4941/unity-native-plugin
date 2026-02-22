# Unity Native Toolkit (Unity 6)

- Unity6 以降でネイティブ機能を提供するツールキットです。
- パッケージには Android/iOS/Windows/macOS 用のネイティブプラグインとサンプルシーンが含まれ、各プラットフォームのダイアログ操作をシングルトン API で扱えます。
- Editor 用ウィンドウからネイティブライブラリや Gradle/Xcode 設定を追加でき、ビルド後のプロジェクト整備をワークフロー化します。

## バージョン

- 1.0.0

## 対応 OS バージョン

- Android 12 以降
- iOS 18 以降
- Windows 11 以降
- macOS 15 以降

## 機能一覧

### Android

- ダイアログ機能
  - 基本ダイアログ
  - 確認ダイアログ
  - シングル選択ダイアログ
  - マルチ選択ダイアログ
  - 入力ダイアログ
  - ログインダイアログ

### iOS

- ダイアログ機能
  - 基本ダイアログ
  - 確認ダイアログ
  - ディストラクティブなダイアログ
  - アクションシート
  - 入力ダイアログ
  - ログインダイアログ

### Windows

- ダイアログ機能
  - 基本ダイアログ
  - ファイル選択ダイアログ
  - 複数ファイル選択ダイアログ
  - フォルダ選択ダイアログ
  - 複数フォルダ選択ダイアログ
  - ファイル保存ダイアログ

### macOS

- ダイアログ機能
  - 基本ダイアログ
  - ファイル選択ダイアログ
  - 複数ファイル選択ダイアログ
  - フォルダ選択ダイアログ
  - 複数フォルダ選択ダイアログ
  - ファイル保存ダイアログ

## 追加予定機能

- シェア機能
- クリップボード連携
- 通知

## インストール

- Unity6 を起動します。
- Window → Package Manager を選択します。
- Unity Package Manager → install from Git URL... を選択します。
- Native Toolkit パッケージの Git URL を入力します。
  - Git URL: https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.0.0
- install をクリックします。
- 必要条件:
  - Unity 6 以降
  - 依存パッケージ: Localization, Addressables, Input System

## サンプル

- Unity6 を起動します。
- Window → Package Manager を選択します。
- Unity Package Manager → Native Toolkit → Samples → Import を選択します。
- Tools → Native Toolkit → Example を選択します。

## 詳細ドキュメント

- 詳細はパッケージ内ドキュメントを参照してください。
  - [英語ドキュメント](docs/latest/index.md)
  - [韓国語ドキュメント](docs/latest/index.ko.md)
  - [日本語ドキュメント](docs/latest/index.ja.md)
