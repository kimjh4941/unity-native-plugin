# 共有機能

言語:

- [English](share.md)
- 日本語（このページ）
- [한국어](share.ko.md)

← [マニュアルトップへ戻る](index.ja.md)

---

## 目次

- [Android](#android)
  - [1.13.0 での変更](#1130-での変更)
  - [セットアップ](#セットアップ)
  - [テキスト共有](#テキスト共有)
  - [URL共有](#url共有)
  - [カスタムChooserアクション付き共有（API 34+）](#カスタムchooserアクション付き共有api-34)
  - [件名・タイトル付き共有](#件名タイトル付き共有)
  - [リッチプレビュー付き共有](#リッチプレビュー付き共有)
  - [画像共有](#画像共有)
  - [複数画像共有](#複数画像共有)
  - [ファイル共有](#ファイル共有)
  - [複数ファイル共有](#複数ファイル共有)
  - [ダイレクト共有ターゲット](#ダイレクト共有ターゲット)
  - [コールバック付き共有](#コールバック付き共有)
  - [保留コールバックのキャンセル](#保留コールバックのキャンセル)
  - [イベントの受信](#イベントの受信)
  - [エラーハンドリング](#エラーハンドリング)
- [iOS](#ios)
  - [セットアップ](#セットアップ-1)
  - [テキスト共有](#テキスト共有-1)
  - [URL共有](#url共有-1)
  - [プレビュー付きURL共有](#プレビュー付きurl共有)
  - [画像共有](#画像共有-1)
  - [複数画像共有](#複数画像共有-1)
  - [ファイル共有](#ファイル共有-1)
  - [複数ファイル共有](#複数ファイル共有-1)
  - [テキストとURLの共有](#テキストとurlの共有)
  - [件名付き共有](#件名付き共有)
  - [特定アクティビティを除外した共有](#特定アクティビティを除外した共有)
  - [結果の受信](#結果の受信)
  - [エラーハンドリング](#エラーハンドリング-1)
- [macOS](#macos)
  - [セットアップ](#セットアップ-2)
  - [テキスト共有](#テキスト共有-2)
  - [URL共有](#url共有-2)
  - [画像共有](#画像共有-2)
  - [ファイル共有](#ファイル共有-2)
  - [複数画像共有](#複数画像共有-2)
  - [複数ファイル共有](#複数ファイル共有-2)
  - [テキストとURLの共有](#テキストとurlの共有-1)
  - [サービスを除外した共有](#サービスを除外した共有)
  - [名前付きサービス経由の共有（Mail）](#名前付きサービス経由の共有mail)
  - [結果の受信](#結果の受信-1)
  - [エラーハンドリング](#エラーハンドリング-2)

---

## Android

### 1.13.0 での変更

1.13.0 から、Android の共有機能は 1.x の Kotlin のブリッジではなく、native-toolkit の C ABI 2.0.0 で動きます。`AndroidShareManager` のメソッドとイベント、Payload のフィールド、結果のプロパティは変わりません。正しい入力で共有すれば、今までと同じ Sharesheet が開きます。下で説明する FileProvider と、ダイレクト共有のマニフェストの設定も変わりません。直すところと、1.12.0 との違いは次のとおりです。

#### コードの直し方

- Android のコードの `using JonghyunKim.NativeToolkit.Runtime.Share;` を `using JonghyunKim.NativeToolkit.Runtime.Android.Share;` に書き換えてください。iOS と macOS のコードの行はそのままにしてください（型は移っていません）。
- 下の表の型の名前を書き換えてください。Unity は自動では直しません。API Updater はこのパッケージの型への参照を書き換えないので、1.12.0 の名前のままのスクリプトはコンパイルに失敗します。このページの例のように `#if UNITY_ANDROID && !UNITY_EDITOR` の中に書いたコードは Editor ではコンパイルされないので、エラーは Android の Player をビルドしたときに初めて出ます。
- 1.12.0 の名前で `[SerializeReference]` に保存したデータは、新しい型として値ごと読み込まれます。改名した型は、1.12.0 の名前を `[MovedFrom]` で記録しています。

Android の共有機能の型は、すべて `JonghyunKim.NativeToolkit.Runtime.Share` から `JonghyunKim.NativeToolkit.Runtime.Android.Share` に移りました。`AndroidShareManager` は名前がそのままで、ほかの型は名前が変わりました。

| 1.12.0 | 1.13.0 |
| --- | --- |
| `ShareOperationResult` | `AndroidShareOperationResult` |
| `ShareCallbackResult` | `AndroidShareCallbackResult` |
| `ShareChooserActionResult` | `AndroidShareChooserActionResult` |
| `ShareChooserActionCallbackCoordinator` | `AndroidShareChooserActionCallbackCoordinator` |
| `ChooserActionPayload` | `AndroidChooserActionPayload` |
| `ShareTextPayload` | `AndroidShareTextPayload` |
| `ShareImagePayload` | `AndroidShareImagePayload` |
| `ShareImagesPayload` | `AndroidShareImagesPayload` |
| `ShareFilePayload` | `AndroidShareFilePayload` |
| `ShareFilesPayload` | `AndroidShareFilesPayload` |
| `DirectShareTargetPayload` | `AndroidDirectShareTargetPayload` |
| `RemoveDirectShareTargetsPayload` | `AndroidRemoveDirectShareTargetsPayload` |

#### Android の全機能に共通の破壊的変更

- **1.x のネイティブライブラリを同梱しなくなりました。** `android-native-toolkit-1.3.0.aar` と `unity-android-native-toolkit-1.3.0.aar` を外しました。パッケージに入っているのは `android-native-toolkit-2.0.0.aar` と `android-native-toolkit-capi-2.0.0.aar`（native-toolkit の C ABI 2.0.0）です。これらのクラス（`android.unity.*`、`android.library.*`）を直接呼んでいたコードは動きません。
- **「Configure Gradle Project」は無くなりました。** メニューの Tools > Native Toolkit > Android > Configure Gradle Project と、そのウィンドウを削除しました。実行時に要るライブラリ（`kotlin-stdlib` 2.2.21、`kotlinx-coroutines-core` 1.9.0 を含む 7 つ）は、ビルドが `unityLibrary/build.gradle` に自分で足します。Kotlin の Gradle プラグインは適用しなくなりました。独自の Gradle のテンプレートや EDM4U を使っている場合は、版がぶつからないか確かめてください。
- **64 ビットと IL2CPP だけに対応します。** ARMv7（32 ビット）も含むビルドはメッセージを出して止まります。Mono には対応しません。[Android の要件](index.ja.md#android-の要件)を見てください。

#### 共有機能のそのほかの変更

- **`AndroidShareJsonBuilder` を削除しました。** 1.12.0 では、`AndroidShareManager` がこのクラスで Payload から JSON を組み立て、Kotlin のブリッジに渡していました。今は Manager が Payload をそのままネイティブライブラリに渡すので、この JSON を使うところはありません。このページの例のように、Payload のオブジェクト（`AndroidShareTextPayload` など）を `AndroidShareManager` のメソッドに渡してください。JSON の文字列に代わるものはありません。
- **既知の違い。** 次の場合は 1.12.0 と動きが違います。多くは、正しくない入力、ほかのスレッドからの呼び出し、立て続けの呼び出しに関わるものです。

| 場面 | 1.12.0 | 1.13.0 |
| --- | --- | --- |
| Unity のメインスレッド以外のスレッドから呼ぶ | 受け付けませんでした。エラーのログが 2 行出て、`<op> could not be started.` で失敗しました | 受け付けます。結果は Unity のメインスレッドで届きます。`AndroidShareManager.Instance` は最初に Unity のメインスレッドで取得してください（最初の取得で GameObject を作ります） |
| アプリがバックグラウンドにあるときに共有する | 測っていません | `Failed to <op>: the app is not in the foreground.` で失敗します |
| `RegisterDirectShareTarget` と `RemoveDirectShareTargets` | Android のメインスレッドで非同期に動きました | 呼んだスレッドで同期に動くので、アイコンの読み込みとシステムの呼び出しの分だけ呼び出しが長くなります。結果は今までどおり後からイベントで届きます |
| 別の呼び出しの結果の順 | 呼んだ順 | 呼んだ時点で決まる結果（検査の失敗、ダイレクト共有、`CancelPendingShareCallback` の成功）が、先に呼んだ共有の結果より先に届くことがあります。たとえば `ShareWithCallback` と同じフレームで `CancelPendingShareCallback` を呼ぶと、取り消しの成功が `onStarted` より先に届くことがあります |
| 呼んだ時点で決まる結果の、呼び出しごとのコールバック | 同じ操作の先の呼び出しの完了が、後の呼び出しのコールバックに届くことがありました | その呼び出しで渡したコールバックに届きます |
| ユーザーがアプリを選んでいる間の `CancelPendingShareCallback` | 待ちを外した後に成功を報告しました | 取り消しを積んだ時点（待ちを外す前）に成功を報告します。同じ瞬間に選ばれたアプリは、その後に届くことがあります |
| 共有のイベントハンドラーやコールバックの中で呼び、結果が C# で決まるもの（Editor でのすべての呼び出しと、端末での検査の失敗） | 同じフレームで結果が届きました | 次のフレーム以降に届きます |
| 複数のスレッドから同時に呼ぶ | Android のメインスレッドで順に処理しました | 呼び出しごとのコールバック、Chooser アクションのタップの届け先、取り消しが、呼んだ順にならないことがあります |
| 画像として読めない Chooser アクションのアイコン | そのアクションを飛ばしました | 先頭のバイトが画像の形式に当たらなければ、そのアクションを飛ばします。当たっても画像が壊れていれば、共有全体が `Failed to shareText: a chooser action icon could not be read as an image.` で失敗します。API 33 以下でも失敗します |
| PNG、JPEG、WebP、GIF、BMP、ICO、HEIF、AVIF 以外の形式（WBMP など）の Chooser アクションのアイコン | 表示しました | そのアクションを飛ばします |
| `intentAction` が同じ Chooser アクション | すべて表示し、どれをタップしても同じ ID を報告しました | 最初の 1 つだけを表示します |
| 前の `ShareText` の Chooser アクション | 次の `ShareText` がネイティブ側に届いたとき、または Manager を破棄したときに止まりました | 次の `ShareText` が成功したときに止まります。次の `ShareText` が失敗したときと、Manager を作り直した後は、前のアクションをまだタップでき、今の `onChooserAction` に届きます |
| 古い Sharesheet で選んだアプリ | 報告しました（不具合） | 報告しません |
| `AndroidShareManager` の破棄の前後に届く結果 | 破棄の前に積まれた結果は破棄した Manager に届き、後のものは捨てました | 結果を配る時点で生きている Manager に届けます（作り直していれば新しい Manager）。いなければ捨てます。Chooser アクションのタップは、タップの時点で生きていた Manager の購読者とコールバックに 1 回届きます |
| 複数のファイル（`ShareImages`、`ShareFiles`）のエラーメッセージ | エラーの原因のパス | `File not found:` は存在しない最初のパスを、`File cannot be shared:` はすべてのパスを示します |
| システムが受け付けないダイレクト共有ターゲット | `Failed to register Direct Share target: <理由>` | `Failed to register Direct Share target: the system did not accept the shortcut.` |
| ネイティブ側のセキュリティの制限や、そのほかの例外 | `Security restriction while executing <op>: ...` のような、例外の文言を含むメッセージ | `Failed to <op>: UNKNOWN.` のような決まったメッセージ |
| null の Payload、ファイルの一覧、ID の一覧、Chooser アクション | `NullReferenceException` | `ArgumentNullException`。何も登録しません |
| `filePaths` や `ids` の null の要素 | `null` というパスとして扱いました（`File not found: null`） | `Failed to <op>: filePaths must not contain null`（`ids` なら `ids must not contain null`）で失敗します |
| 必須の文字列（`text`、`filePath`、`id` など）の null と、Chooser アクションの `label`、`iconBase64`、`intentAction` の null | 測っていません | 空の値と同じように失敗します（たとえば `Failed to <op>: text is required`）。null の値を持つ Chooser アクションは捨てます |
| Base64 のアイコン（`iconBase64`） | Android の `Base64.DEFAULT` で読みました | パディング付きの標準の Base64 だけを読みます。前後の空白は無視します。`Convert.ToBase64String` の出力は読めます |
| 文字列の途中の U+0000 | そのまま渡しました | そこで切れます（U+0000 で始まる `text` は `text is required` で失敗します） |
| 対になっていないサロゲート | そのまま渡しました | U+FFFD に置き換わります |
| Android 以外のビルドターゲットの Editor | `AndroidShareManager` がありませんでした | あります。どの呼び出しも `<op> could not be started.` で失敗します |
| ログ | 本文や Base64 のアイコンを含む JSON 全体 | 本文と `subject` は長さだけを出します |
| `Initialize()` | ブリッジを取得し、リスナーを登録しました | リスナーを登録するだけです。何度呼んでもかまいません |

### セットアップ

#### 名前空間のインポート

```csharp
// ガード: Android（Player）専用。エディター上でのネイティブ呼び出しを防ぎます。
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Android.Share;
#endif
```

#### AndroidManifest.xml（ダイレクト共有に必要）

アプリを Android 共有シートのダイレクト共有ターゲットとして表示させるには、Android ライブラリプロジェクトのマニフェスト（例: `Assets/Plugins/Android/<your-app>.androidlib/AndroidManifest.xml`）に以下を追加します。`RegisterDirectShareTarget` を使用する場合のみ必要です。

```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    package="com.example.your.androidlib">
    <application>
        <activity android:name="com.unity3d.player.UnityPlayerGameActivity" android:exported="true">
            <intent-filter>
                <action android:name="android.intent.action.SEND" />
                <category android:name="android.intent.category.DEFAULT" />
                <data android:mimeType="*/*" />
            </intent-filter>
            <meta-data
                android:name="android.app.shortcuts"
                android:resource="@xml/shortcuts" />
        </activity>
    </application>
</manifest>
```

同じ Android ライブラリプロジェクトに `res/xml/shortcuts.xml` を追加します:

```xml
<?xml version="1.0" encoding="utf-8"?>
<shortcuts xmlns:android="http://schemas.android.com/apk/res/android">
    <share-target android:targetClass="com.unity3d.player.UnityPlayerGameActivity">
        <data android:mimeType="*/*" />
        <category android:name="android.shortcut.conversation" />
    </share-target>
</shortcuts>
```

#### 画像・ファイル共有時のファイルパス

`ShareImage`、`ShareImages`、`ShareFile`、`ShareFiles` に渡すファイルは、ネイティブ FileProvider が対象とするディレクトリに存在する必要があります。`Application.persistentDataPath`（外部ファイルディレクトリにマップされ、FileProvider 設定で `<external-files-path>` として宣言済み）を使用してください。`Application.temporaryCachePath`（外部キャッシュディレクトリ）は FileProvider の対象外のため使用できません。

```csharp
string path = Path.Combine(Application.persistentDataPath, "my_image.png");
```

---

### テキスト共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "Hello from Unity! プレーンテキスト共有のサンプルです。"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareText.png" alt="Example_AndroidShareManager_ShareText" width="400" />
</p>

---

### URL共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "https://unity.com",
    mimeType = "text/plain"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareUrl.png" alt="Example_AndroidShareManager_ShareUrl" width="400" />
</p>

---

### カスタムChooserアクション付き共有（API 34+）

カスタムChooserアクションは、Android 共有チューザーダイアログに追加ボタンとして表示されます。Android 14（API 34）以降が必要です。それ以前のAPIレベルでは、カスタムアクションなしで共有が実行されます。

各アクションには `label`、`iconBase64`（PNG や JPEG などの画像を標準の Base64 にしたもの）、`intentAction` が必要です。どれかが欠けたアクションは表示されません。`intentAction` には、アクションごとに `android.intent.action.SEND` 以外の一意の値を指定してください。同じ値のアクションは最初の 1 つだけが表示され、`SEND` のアクションはタップしても報告されません。タップイベントは `ShareChooserActionTapped`（グローバルイベント）または `ShareText` の `onChooserAction` パラメーター（呼び出しごとのコールバック）で受信できます。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// Chooserアクションのタップを受信するためにグローバルイベントを購読します。
AndroidShareManager.Instance.ShareChooserActionTapped += result =>
{
    Debug.Log($"Chooserアクションがタップされました: {result.ActionId}");
};

AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "カスタムChooserアクション付き共有（Android 14 / API 34+ のみ）。",
    chooserActions = new[]
    {
        new AndroidChooserActionPayload
        {
            label = "保存",
            iconBase64 = iconBase64, // base64エンコードされたPNG/JPEG
            intentAction = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE"
        },
        new AndroidChooserActionPayload
        {
            label = "開く",
            iconBase64 = iconBase64,
            intentAction = "com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_OPEN"
        }
    }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareCustomAction.png" alt="Example_AndroidShareManager_ShareCustomAction" width="400" />
</p>

> **注意:** Android が表示する Chooser アクションは最大 5 個です。6 個以上渡すと警告がログに出て、余分なアクションは表示されないことがあります。`ShareWithCallback` は Chooser アクションをサポートしていません。代わりに `ShareText` に `chooserActions` を指定してください。

---

### 件名・タイトル付き共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "Unity から件名とタイトル付きで共有するサンプルです。",
    title = "Unity 共有サンプル",
    subject = "サンプル件名",
    mimeType = "text/plain"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareWithSubjectTitle.png" alt="Example_AndroidShareManager_ShareWithSubjectTitle" width="400" />
</p>

---

### リッチプレビュー付き共有

チューザーにプレビュータイトルとサムネイル画像を表示します。サムネイルのパスは FileProvider がアクセスできるディレクトリのファイルを指定する必要があります。そうでない場合、チューザーはサムネイルなしで開きます。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string thumbnailPath = Path.Combine(Application.persistentDataPath, "share_preview_thumbnail.png");
// ShareText を呼び出す前に、thumbnailPath へPNGファイルを書き込んでください。

AndroidShareManager.Instance.ShareText(new AndroidShareTextPayload
{
    text = "Unity からのリッチプレビュー共有サンプルです！",
    previewTitle = "Unity リッチプレビューサンプル",
    previewThumbnailPath = thumbnailPath
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareRichPreview.png" alt="Example_AndroidShareManager_ShareRichPreview" width="400" />
</p>

---

### 画像共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string imagePath = Path.Combine(Application.persistentDataPath, "share_sample_image.png");
// ShareImage を呼び出す前に、imagePath へPNGファイルを書き込んでください。

AndroidShareManager.Instance.ShareImage(new AndroidShareImagePayload
{
    filePath = imagePath,
    mimeType = "image/png"
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareImage.png" alt="Example_AndroidShareManager_ShareImage" width="400" />
</p>

---

### 複数画像共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string imagePath1 = Path.Combine(Application.persistentDataPath, "share_sample_image_1.png");
string imagePath2 = Path.Combine(Application.persistentDataPath, "share_sample_image_2.png");
// ShareImages を呼び出す前に、各パスへPNGファイルを書き込んでください。

AndroidShareManager.Instance.ShareImages(new AndroidShareImagesPayload
{
    filePaths = new[] { imagePath1, imagePath2 }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareImages.png" alt="Example_AndroidShareManager_ShareImages" width="400" />
</p>

---

### ファイル共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string filePath = Path.Combine(Application.persistentDataPath, "share_sample_file.txt");
File.WriteAllText(filePath, "Unity Native Toolkit から共有されたサンプルテキストファイルです。");

AndroidShareManager.Instance.ShareFile(new AndroidShareFilePayload
{
    filePath = filePath
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareFile.png" alt="Example_AndroidShareManager_ShareFile" width="400" />
</p>

---

### 複数ファイル共有

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
string filePath1 = Path.Combine(Application.persistentDataPath, "share_sample_file_1.txt");
string filePath2 = Path.Combine(Application.persistentDataPath, "share_sample_file_2.txt");
File.WriteAllText(filePath1, "Unity Native Toolkit のサンプルファイル 1。");
File.WriteAllText(filePath2, "Unity Native Toolkit のサンプルファイル 2。");

AndroidShareManager.Instance.ShareFiles(new AndroidShareFilesPayload
{
    filePaths = new[] { filePath1, filePath2 }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareFiles.png" alt="Example_AndroidShareManager_ShareFiles" width="400" />
</p>

---

### ダイレクト共有ターゲット

Android 共有シートのダイレクト共有行に表示されるショートカットを登録します。[セットアップ](#セットアップ)で説明したマニフェストと `shortcuts.xml` の設定が必要です。OS レベルのキャッシュにより、登録直後にショートカットが表示されない場合があります。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.RegisterDirectShareTarget(new AndroidDirectShareTargetPayload
{
    id = "native_toolkit_sample_target",
    label = "Unity サンプルターゲット",
    iconBase64 = iconBase64 // base64エンコードされたPNG/JPEG、小さいサイズを推奨（64x64）
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_RegisterDirectShareTarget.png" alt="Example_AndroidShareManager_RegisterDirectShareTarget" width="400" />
</p>

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.RemoveDirectShareTargets(new AndroidRemoveDirectShareTargetsPayload
{
    ids = new[] { "native_toolkit_sample_target" }
});
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_RemoveDirectShareTargets.png" alt="Example_AndroidShareManager_RemoveDirectShareTargets" width="400" />
</p>

> **注意:** アイコンビットマップは Android Binder 経由で送信されます。Binder トランザクションサイズの上限を超えないよう、小さなサイズ（64×64 ピクセル推奨）に抑えてください。

---

### コールバック付き共有

テキストを共有し、ユーザーがアプリを選択したときにコールバックを受信します。`onStarted` はチューザーが起動したとき（成功・失敗を問わず）に発火します。`onSelected` はユーザーがアプリを選択したときに発火します（キャンセル・コピー・編集の場合は発火しません）。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.ShareWithCallback(
    new AndroidShareTextPayload
    {
        text = "Unity からのコールバック付き共有サンプルです。アプリを選択すると選択結果を受信します。"
    },
    onStarted: result =>
    {
        string status = result.IsSuccess ? "成功" : "失敗";
        Debug.Log($"[onStarted] ShareWithCallback: {status}");
    },
    onSelected: result =>
    {
        string pkg = result.SelectedPackageName ?? "（不明）";
        Debug.Log($"[onSelected] 選択されたアプリ: {pkg}");
    });
#endif
```

<p align="center">
    <img src="images/android/share/Example_AndroidShareManager_ShareWithCallback.png" alt="Example_AndroidShareManager_ShareWithCallback" width="400" />
</p>

> **注意:** `ShareWithCallback` では `chooserActions` はサポートされていません。代わりに `ShareText` に `chooserActions` を指定してください。

---

### 保留コールバックのキャンセル

`ShareWithCallback` で始めたアプリ選択の待ちをキャンセルします。例えば共有シートが選択なしで閉じられた後など、`onSelected` コールバックを受信したくない場合に呼び出します。Manager は自動では呼び出しません。画面遷移後に古いコールバックが残らないよう、[イベントの受信](#イベントの受信)の例のように `OnDisable` で呼び出してください。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
AndroidShareManager.Instance.CancelPendingShareCallback();
#endif
```

---

### イベントの受信

画面遷移後に古い参照が残らないよう、`OnEnable` でイベントを購読し、`OnDisable` で購読解除してください。結果はすべて Unity のメインスレッドで、まずイベント、次に呼び出しごとのコールバックの順に届きます。

```csharp
private void OnEnable()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidShareManager.Instance.ShareOperationCompleted += OnShareOperationCompleted;
    AndroidShareManager.Instance.ShareCallbackReceived += OnShareCallbackReceived;
    AndroidShareManager.Instance.ShareChooserActionTapped += OnShareChooserActionTapped;
#endif
}

private void OnDisable()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidShareManager.Instance.ShareOperationCompleted -= OnShareOperationCompleted;
    AndroidShareManager.Instance.ShareCallbackReceived -= OnShareCallbackReceived;
    AndroidShareManager.Instance.ShareChooserActionTapped -= OnShareChooserActionTapped;
    AndroidShareManager.Instance.CancelPendingShareCallback();
#endif
}

#if UNITY_ANDROID && !UNITY_EDITOR
private void OnShareOperationCompleted(AndroidShareOperationResult result)
{
    string status = result.IsSuccess ? "成功" : "失敗";
    Debug.Log($"[event] {result.Operation}: {status}");
    if (!string.IsNullOrEmpty(result.ErrorMessage))
        Debug.LogError(result.ErrorMessage);
}

private void OnShareCallbackReceived(AndroidShareCallbackResult result)
{
    string pkg = result.SelectedPackageName ?? "（不明）";
    Debug.Log($"[event] ShareCallback: {pkg} が選択されました");
}

private void OnShareChooserActionTapped(AndroidShareChooserActionResult result)
{
    Debug.Log($"[event] Chooserアクションがタップされました: {result.ActionId}");
}
#endif
```

---

### エラーハンドリング

すべての共有操作は `ShareOperationCompleted`（と呼び出しごとのコールバック）を通じて成功・失敗を報告します。`IsSuccess` が `false` の場合、`ErrorMessage` フィールドに説明が含まれます。null の Payload、ファイルの一覧、ID の一覧、Chooser アクションはこの方法では報告されず、メソッドが `ArgumentNullException` を投げます。

共有 API に渡すファイルは存在し、FileProvider がアクセスできるディレクトリにある必要があります。存在しないパスは `File not found: <path>` で、そのディレクトリの外のファイルは `File cannot be shared: <path>. Ensure the file is in a supported directory.` で失敗します。

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
// ShareOperationCompleted でエラーが報告されます（IsSuccess = false）
// ErrorMessage: "File not found: /invalid/path/that/does/not/exist/sample.txt"
AndroidShareManager.Instance.ShareFile(new AndroidShareFilePayload
{
    filePath = "/invalid/path/that/does/not/exist/sample.txt"
});
#endif
```

---

## iOS

### セットアップ

#### 名前空間のインポート

`IosShareManager` は iOS ビルドターゲットが選択されている限り、エディターを含めて常にコンパイルされます。エディター上や非 iOS デバイスで `Share` を呼び出してもクラッシュせず、即座に失敗結果が返されます（[エラーハンドリング](#エラーハンドリング-1)参照）。

```csharp
#if UNITY_IOS || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Share;
#endif
```

#### 画像・ファイル共有時のパスについて

Android と異なり、iOS には FileProvider の制約がありません。`Application.persistentDataPath` 配下のファイルはそのまま共有できます。

```csharp
string path = Path.Combine(Application.persistentDataPath, "my_image.png");
```

---

### テキスト共有

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Text("Shared from Unity Native Toolkit") }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareText.png" alt="Example_IosShareManager_ShareText" width="400" />
</p>

---

### URL共有

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareUrl.png" alt="Example_IosShareManager_ShareUrl" width="400" />
</p>

---

### プレビュー付きURL共有

`previewTitle` を指定すると、共有シートのコンテンツプレビュー領域に表示されるタイトルを設定できます。

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("https://unity.com") },
    previewTitle = "Unity"
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareUrlPreview.png" alt="Example_IosShareManager_ShareUrlPreview" width="400" />
</p>

---

### 画像共有

```csharp
#if UNITY_IOS || UNITY_EDITOR
string imagePath = Path.Combine(Application.persistentDataPath, "share_sample_image.png");
// Share を呼び出す前に、imagePath に PNG ファイルを書き出しておきます。

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Image(imagePath) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareImage.png" alt="Example_IosShareManager_ShareImage" width="400" />
</p>

---

### 複数画像共有

```csharp
#if UNITY_IOS || UNITY_EDITOR
string imagePath1 = Path.Combine(Application.persistentDataPath, "share_sample_image_1.png");
string imagePath2 = Path.Combine(Application.persistentDataPath, "share_sample_image_2.png");
// Share を呼び出す前に、各パスに PNG ファイルを書き出しておきます。

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Image(imagePath1), IosShareItem.Image(imagePath2) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareImages.png" alt="Example_IosShareManager_ShareImages" width="400" />
</p>

---

### ファイル共有

```csharp
#if UNITY_IOS || UNITY_EDITOR
string filePath = Path.Combine(Application.persistentDataPath, "share_sample_file.txt");
File.WriteAllText(filePath, "This is a sample text file shared from Unity Native Toolkit.");

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.File(filePath) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareFile.png" alt="Example_IosShareManager_ShareFile" width="400" />
</p>

---

### 複数ファイル共有

```csharp
#if UNITY_IOS || UNITY_EDITOR
string filePath1 = Path.Combine(Application.persistentDataPath, "share_sample_file_1.txt");
string filePath2 = Path.Combine(Application.persistentDataPath, "share_sample_file_2.txt");
File.WriteAllText(filePath1, "Sample file 1 from Unity Native Toolkit.");
File.WriteAllText(filePath2, "Sample file 2 from Unity Native Toolkit.");

IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.File(filePath1), IosShareItem.File(filePath2) }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareFiles.png" alt="Example_IosShareManager_ShareFiles" width="400" />
</p>

---

### テキストとURLの共有

1回の `Share` 呼び出しで、異なる種類の複数アイテムをまとめて共有できます。

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Text("Check this out"), IosShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareMultiple.png" alt="Example_IosShareManager_ShareMultiple" width="400" />
</p>

---

### 件名付き共有

`subject` は、Mail などのアクティビティでメッセージの件名として使用されます。

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Text("Body text") },
    subject = "Sample Subject"
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareWithSubject.png" alt="Example_IosShareManager_ShareWithSubject" width="400" />
</p>

---

### 特定アクティビティを除外した共有

`excludedActivityTypes` を指定すると、共有シートから指定したアクティビティタイプ（raw identifier）を非表示にできます。

```csharp
#if UNITY_IOS || UNITY_EDITOR
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("https://unity.com") },
    excludedActivityTypes = new[]
    {
        "com.apple.UIKit.activity.CopyToPasteboard",
        "com.apple.UIKit.activity.PostToFacebook"
    }
});
#endif
```

<p align="center">
    <img src="images/ios/share/Example_IosShareManager_ShareExcludingActivities.png" alt="Example_IosShareManager_ShareExcludingActivities" width="400" />
</p>

> **注記:** 特定のアクティビティタイプが実際に共有シートに表示されるかは、端末やインストール済みアプリにも依存します。Facebook のようなサードパーティアクティビティは検証端末にインストールされていない場合があるため、`com.apple.UIKit.activity.CopyToPasteboard`（コピー）を主な確認対象としてください。

---

### 結果の受信

画面遷移後の古い参照を避けるため、`OnEnable` で `ShareCompleted` を購読し、`OnDisable` で解除します。`ShareCompleted` は、`Share` に渡した任意の個別コールバックより必ず先に発火します。

```csharp
private void OnEnable()
{
#if UNITY_IOS || UNITY_EDITOR
    IosShareManager.Instance.ShareCompleted += OnShareCompleted;
#endif
}

private void OnDisable()
{
#if UNITY_IOS || UNITY_EDITOR
    IosShareManager.Instance.ShareCompleted -= OnShareCompleted;
#endif
}

#if UNITY_IOS || UNITY_EDITOR
private void OnShareCompleted(IosShareResult result)
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"Share failed: {result.ErrorMessage}");
        return;
    }

    if (result.Completed)
    {
        Debug.Log($"Share completed: activityType={result.ActivityType}");
    }
    else
    {
        Debug.Log("Share cancelled by the user.");
    }
}
#endif
```

> **注記:** ユーザーによるキャンセルはエラーとして扱われません。`IsSuccess` は `true`、`Completed` は `false` となり、`ActivityType` は `null` になります。

---

### エラーハンドリング

すべての `Share` 呼び出しの結果は `ShareCompleted`（および `Share` に渡した任意の個別コールバック）を通じて報告されます。`IsSuccess` が `false` の場合、`ErrorMessage` は必ず non-null になります。

```csharp
#if UNITY_IOS || UNITY_EDITOR
// アイテムなし: 共有シートを提示せずに即座に失敗します。
// ErrorMessage: "No shareable items were provided."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = Array.Empty<IosShareItem>()
});

// 不正なURL文字列: ネイティブ側のバリデーションエラーで失敗します。
// ErrorMessage: "Invalid URL: not a valid url."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Url("not a valid url") }
});

// ファイル不在: ネイティブ側がファイルを見つけられない場合に失敗します。
// ErrorMessage: "File not found at path: /nonexistent/share-missing.txt."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.File("/nonexistent/share-missing.txt") }
});

// 画像不在: ネイティブ側が画像を読み込めない場合に失敗します。
// ErrorMessage: "Failed to load image at path: /nonexistent/share-missing.png."
IosShareManager.Instance.Share(new IosShareContentPayload
{
    items = new[] { IosShareItem.Image("/nonexistent/share-missing.png") }
});
#endif
```

---

## macOS

macOS の共有機能は `MacShareManager` を通じて提供されます。2つのエントリーポイントを公開しています。`Share` はシステムの共有サービスピッカー（`NSSharingServicePicker`）を提示し、`ShareViaService` はピッカーを表示せずに単一の名前付きサービスを直接実行します。

> **注意:** `Share`（ピッカー）は、`NSSharingServicePicker.show(...)` が `mouseDown` イベントコンテキストを要求するため、ボタンクリックなどユーザー起点のアクションから呼び出す必要があります。`ShareViaService` にはこの制約がなく、確定的な結果が必要な場合（例: 常に Mail 経由で共有する）はより信頼できる方法です。

### セットアップ

#### 名前空間のインポート

`MacShareManager` は macOS Standalone ビルドターゲットが選択されている限り、エディタを含めてコンパイルされます。エディタや非 macOS プレイヤー上で `Share` や `ShareViaService` を呼び出してもクラッシュせず、代わりに即座に失敗結果が返ります（[エラーハンドリング](#エラーハンドリング-2)を参照）。

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Share;
#endif
```

#### 画像・ファイル共有時のパス

macOS には Android のような FileProvider 制約はありません。`Application.persistentDataPath` 配下の任意のファイルを直接共有できます。

```csharp
string path = Path.Combine(Application.persistentDataPath, "my_image.png");
```

---

### テキスト共有

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Shared from Unity Native Toolkit") }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareText.png" alt="Example_MacShareManager_ShareText" width="800" />
</p>

---

### URL共有

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareUrl.png" alt="Example_MacShareManager_ShareUrl" width="800" />
</p>

---

### 画像共有

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string imagePath = Path.Combine(Application.persistentDataPath, "share_sample_image.png");
// Share を呼ぶ前に imagePath へ PNG ファイルを書き込みます。

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Image(imagePath) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareImage.png" alt="Example_MacShareManager_ShareImage" width="800" />
</p>

---

### ファイル共有

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string filePath = Path.Combine(Application.persistentDataPath, "share_sample_file.txt");
File.WriteAllText(filePath, "This is a sample text file shared from Unity Native Toolkit.");

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.File(filePath) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareFile.png" alt="Example_MacShareManager_ShareFile" width="800" />
</p>

---

### 複数画像共有

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string imagePath1 = Path.Combine(Application.persistentDataPath, "share_sample_image_1.png");
string imagePath2 = Path.Combine(Application.persistentDataPath, "share_sample_image_2.png");
// Share を呼ぶ前に各パスへ PNG ファイルを書き込みます。

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Image(imagePath1), MacShareItem.Image(imagePath2) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareImages.png" alt="Example_MacShareManager_ShareImages" width="800" />
</p>

---

### 複数ファイル共有

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
string filePath1 = Path.Combine(Application.persistentDataPath, "share_sample_file_1.txt");
string filePath2 = Path.Combine(Application.persistentDataPath, "share_sample_file_2.txt");
File.WriteAllText(filePath1, "Sample file 1 from Unity Native Toolkit.");
File.WriteAllText(filePath2, "Sample file 2 from Unity Native Toolkit.");

MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.File(filePath1), MacShareItem.File(filePath2) }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareFiles.png" alt="Example_MacShareManager_ShareFiles" width="800" />
</p>

---

### テキストとURLの共有

異なる種類の複数アイテムを1回の `Share` 呼び出しでまとめて共有できます。

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Check this out"), MacShareItem.Url("https://unity.com") }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareTextAndUrl.png" alt="Example_MacShareManager_ShareTextAndUrl" width="800" />
</p>

---

### サービスを除外した共有

`excludedServiceTitles` は、指定した文字列と一致する表示タイトルを持つサービスを非表示にします（`NSSharingService.title` に対するベストエフォートマッチ）。

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Url("https://unity.com") },
    excludedServiceTitles = new[] { "Add to Reading List" }
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareExcludingServices.png" alt="Example_MacShareManager_ShareExcludingServices" width="800" />
</p>

---

### 名前付きサービス経由の共有（Mail）

`ShareViaService` はピッカーを表示せずに、単一の名前付き共有サービスを直接実行します。`recipients` と `subject` は、対象サービスがこれらをサポートする場合（例: Mail）に適用されます。既知の raw サービス識別子には `MacShareServiceNames` を使用します。

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
MacShareManager.Instance.ShareViaService(MacShareServiceNames.MailCompose, new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Body text") },
    recipients = new[] { "test@example.com" },
    subject = "Sample Subject"
});
#endif
```

<p align="center">
    <img src="images/mac/share/Example_MacShareManager_ShareViaMail.png" alt="Example_MacShareManager_ShareViaMail" width="800" />
</p>

> **注意:** `MacShareServiceNames` には `MailCompose` など、既知の raw `NSSharingService.Name` 識別子が定義されています。これらは `ShareViaService` への入力用識別子であり、`MacShareResult.ServiceName` で返される表示名ではありません。

---

### 結果の受信

`OnEnable` で `ShareCompleted` を購読し、`OnDisable` で解除することで、画面遷移後の古い参照を避けられます。`ShareCompleted` は `Share` と `ShareViaService` の両方で発火し、どちらのメソッドに渡した個別コールバックよりも常に先に発火します。

```csharp
private void OnEnable()
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
    MacShareManager.Instance.ShareCompleted += OnShareCompleted;
#endif
}

private void OnDisable()
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
    MacShareManager.Instance.ShareCompleted -= OnShareCompleted;
#endif
}

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
private void OnShareCompleted(MacShareResult result)
{
    if (!result.IsSuccess)
    {
        Debug.LogError($"共有に失敗しました: {result.ErrorMessage}");
        return;
    }

    if (result.Completed)
    {
        Debug.Log($"共有が完了しました: service={result.ServiceName}");
    }
    else
    {
        Debug.Log("ユーザーが共有をキャンセルしました。");
    }
}
#endif
```

> **注意:** ユーザーによるキャンセルはエラーではありません。`IsSuccess` は `true`、`Completed` は `false` となり、`ServiceName` は `null` になります。

---

### エラーハンドリング

すべての `Share` / `ShareViaService` 呼び出しは、結果を `ShareCompleted`（および任意の個別コールバック）経由で通知します。`IsSuccess` が `false` のとき、`ErrorMessage` は必ず非 null です。

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// アイテムなし: ピッカーを表示せず即座に失敗します。
// ErrorMessage: "No shareable items were provided."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = Array.Empty<MacShareItem>()
});

// 無効なURL文字列: ネイティブ側の検証エラーで失敗します。
// ErrorMessage: "Invalid URL: not a valid url."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Url("not a valid url") }
});

// ファイル不在: ネイティブ側がファイルを見つけられない場合に失敗します。
// ErrorMessage: "File not found at path: /nonexistent/share-missing.txt."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.File("/nonexistent/share-missing.txt") }
});

// 画像不在: ネイティブ側が画像を読み込めない場合に失敗します。
// ErrorMessage: "Failed to load image at path: /nonexistent/share-missing.png."
MacShareManager.Instance.Share(new MacShareContentPayload
{
    items = new[] { MacShareItem.Image("/nonexistent/share-missing.png") }
});

// 不明なサービス: 指定したサービスが存在しない、または実行できない場合に失敗します。
// ErrorMessage: "Sharing service unavailable: invalid.service."
MacShareManager.Instance.ShareViaService("invalid.service", new MacShareContentPayload
{
    items = new[] { MacShareItem.Text("Body text") }
});
#endif
```
