# Unity Native Toolkit (Unity 6)

[English](index.md) | [Korean](index.ko.md) | [Japanese](index.ja.md)

- Unity6 以降でネイティブ機能を提供するツールキットです。
- パッケージには Android/iOS/Windows/macOS 用のネイティブプラグインとサンプルシーンが含まれ、各プラットフォームのダイアログ操作をシングルトン API で扱えます。
- Editor 用ウィンドウからネイティブライブラリや Gradle/Xcode 設定を追加でき、ビルド後のプロジェクト整備をワークフロー化します。

# バージョン

## 1.0.0

# 対応 OS バージョン

- Android 12 以降
- iOS 18 以降
- Windows 11 以降
- macOS 15 以降

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

- シェア機能
- クリップボード連携
- 通知

# はじめに

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
<p align="center">
  <img src="images/editor/NativeToolkitEditorWindow.png" alt="NativeToolkit Editor Window" width="720" />
</p>

- Android サンプル
  - Android - Dialog - AndroidDialogManager.cs を選択します。
  - 「Open」ボタンをクリックします。
  - 「Game ビュー」にサンプル画面が表示されます。
  - 「Build Profiles」から「Android Profile」→ Export を実行します。
  - Tools → Native Toolkit → Android → Configue Gradle Project を選択します。
  <p align="center">
    <img src="images/editor/ConfigureGradleProject.png" alt="ConfigureGradleProject" width="720" />
  </p>　

  - 「Browse」ボタンを押下して、Export した Android Project を指定します。
  - 「Run: Add Kotlin Dependencies」ボタンを押下して、Kotlin ライブラリを追加します。
  - Android Studio からサンプルアプリをインストールしてください。
    - <a href="https://developer.android.com/studio?hl=ja" target="_blank" rel="noopener noreferrer">参考サイト</a>

- iOS サンプル
  - iOS - Dialog - IosDialogManager.cs を選択します。
  - 「Open」ボタンをクリックします。
  - 「Game ビュー」にサンプル画面が表示されます。
  - 「Build Profiles」から「iOS Profile」→ Build を実行します。
  - Tools → Native Toolkit → iOS → Configue Xcode Project を選択します。
  <p align="center">
    <img src="images/editor/IosConfigureXcodeProject.png" alt="IosConfigureXcodeProject" width="720" />
  </p>

  - 「Browse」ボタンを押下して、Build した iOS Project を指定します。
  - 「Run: Add/Embed iOS XCFrameworks」ボタンを押下して、NativeToolkit ライブラリを追加します。
  - Xcode からサンプルアプリをインストールしてください。
    - <a href="https://developer.apple.com/jp/xcode" target="_blank" rel="noopener noreferrer">参考サイト</a>

- Windows サンプル
  - Windows - Dialog - WindowsDialogManager.cs を選択します。
  - 「Open」ボタンをクリックします。
  - 「Game ビュー」にサンプル画面が表示されます。
  - 「Build Profiles」から「Windows Profile」→ Build を実行します。
  - Build 出力先にある「Unity NativeToolkit.exe」を実行してください。

- Mac サンプル
  - macOS - Dialog - MacDialogManager.cs を選択します。
  - 「Open」ボタンをクリックします。
  - 「Game ビュー」にサンプル画面が表示されます。
  - 「Build Profiles」から「macOS Profile」→ Build を実行します。
  - Tools → Native Toolkit → macOS → Configue Xcode Project を選択します。
  <p align="center">
    <img src="images/editor/MacConfigureXcodeProject.png" alt="MacConfigureXcodeProject" width="720" />
  </p>

  - 「Browse」ボタンを押下して、Build した macOS Project を指定します。
  - 「Run: Add UnityMacNativeToolkit.xcframework」ボタンを押下して、NativeToolkit ライブラリを追加します。
  - Xcode からサンプルアプリをインストールしてください。
    - <a href="https://developer.apple.com/jp/xcode" target="_blank" rel="noopener noreferrer">参考サイト</a>


# API 使用方法

- [ダイアログ機能](dialog.ja.md)
- [通知機能](notification.ja.md)
