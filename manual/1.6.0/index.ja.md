# Unity Native Toolkit (Unity 6)

[English](index.md) | [Korean](index.ko.md) | [Japanese](index.ja.md)

- Unity6 以降でネイティブ機能を提供するツールキットです。
- パッケージには Android/iOS/Windows/macOS 用のネイティブプラグインとサンプルシーンが含まれ、ダイアログ・通知・共有などのネイティブ機能をシングルトン API で扱えます。
- Editor 用ウィンドウからネイティブライブラリや Gradle/Xcode 設定を追加でき、ビルド後のプロジェクト整備をワークフロー化します。

# バージョン

## 1.6.0

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

- 通知機能
  - 即座通知
  - スケジュール通知
  - 進捗表示フォアグラウンドサービス
  - 通知アクション
  - フルスクリーン表示
  - カスタムビュー

- 共有機能
  - テキスト / URL 共有
  - 画像 / 複数画像共有
  - ファイル / 複数ファイル共有
  - カスタム Chooser アクション（API 34+）
  - リッチプレビュー
  - ダイレクト共有ターゲット登録
  - コールバック付き共有
  - 保留コールバックのキャンセル

## iOS

- ダイアログ機能
  - 基本ダイアログ
  - 確認ダイアログ
  - ディストラクティブなダイアログ
  - アクションシート
  - 入力ダイアログ
  - ログインダイアログ
- 通知機能
  - 通知権限リクエスト / 権限状態確認 / 通知設定画面遷移
  - 即座通知（添付ファイル付き通知を含む）
  - スケジュール通知（時間間隔 / カレンダー / 位置情報）
  - 通知更新 / キャンセル / 配信済み削除 / 状態取得
  - バッジ数設定
  - カテゴリ登録 / アクション / テキスト入力アクション
- 共有機能
  - テキスト / URL / プレビュー付きURL共有
  - 画像 / 複数画像共有
  - ファイル / 複数ファイル共有
  - テキストとURLの同時共有
  - 件名付き共有
  - 共有シートから特定アクティビティを除外

## Windows

- ダイアログ機能
  - 基本ダイアログ
  - ファイル選択ダイアログ
  - 複数ファイル選択ダイアログ
  - フォルダ選択ダイアログ
  - 複数フォルダ選択ダイアログ
  - ファイル保存ダイアログ

- 通知機能
  - 即時通知
  - スケジュール通知
  - スケジュール通知のキャンセル
  - 進捗バー通知の表示と更新
  - タグ指定または全件での通知削除
  - 通知許可設定のクエリ
  - システム通知設定を開く
  - 通知アクティベーションイベントの受信（コールドスタート起動を含む）

## Mac

- ダイアログ機能
  - 基本ダイアログ
  - ファイル選択ダイアログ
  - 複数ファイル選択ダイアログ
  - フォルダ選択ダイアログ
  - 複数フォルダ選択ダイアログ
  - ファイル保存ダイアログ

- 通知機能
  - 通知権限リクエスト / 権限状態確認 / システム通知設定を開く
  - 即座通知
  - スケジュール通知（時間間隔 / カレンダー）
  - 通知更新 / キャンセル / 配信済み通知の削除
  - スケジュール済み・配信済み通知の一覧取得
  - バッジ数設定
  - カテゴリ登録 / アクション / テキスト入力アクション

## 追加予定機能

- クリップボード連携

# はじめに

## インストール

- Unity6 を起動します。
- Window → Package Manager を選択します。
- Unity Package Manager → install from Git URL... を選択します。
- Native Toolkit パッケージの Git URL を入力します。
  - Git URL: https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.6.0
- install をクリックします。
- 必要条件:
  - Unity 6 以降
  - 依存パッケージ: Localization, Addressables, Input System

## サンプル

- Unity6 を起動します。
- Window → Package Manager を選択します。
- Unity Package Manager → Native Toolkit → Samples → Import を選択します。
- Tools → Native Toolkit → Sample を選択します。
  <p align="center">
      <img src="images/editor/NativeToolkitSample.png" alt="NativeToolkitSample" width="720" />
  </p>

- Android サンプル
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
  - 「Game ビュー」にサンプル画面が表示されます。
  - 「Build Profiles」から「Windows Profile」→ Build を実行します。
  - Build 出力先にある「Unity NativeToolkit.exe」を実行してください。

- Mac サンプル
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
- [共有機能](share.ja.md)
