# Android の Player テストから OS の画面を操作する補助（ホストの adb）v1

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android の層 2b（実機の Player の PlayMode テスト）で、ダイアログ・Share の選択画面・通知の許可とシェードを押す、読む |
| 最初の利用者 | Dialog（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v1.md` 7.3、8.3） |
| 後の利用者 | Share、Notification（親課題の「進め方」の段 3） |
| 方針の出典 | `agent-rules/coding-rules/testing.md` 2 節（Android は「Appium + UiAutomator2、または UiAutomator」）。Windows は同じ節の考え方で、Appium ではなく PowerShell の Win32 メッセージを選んだ（`artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`） |

## 1. 何を作るか

ホストの Python のスクリプト `scripts/android_ui_driver.py` が、テストの実行の間 `adb logcat` を読み、テストからの依頼の行に応じて `adb shell uiautomator dump` と `adb shell input` で画面を操作し、終わったことを端末のファイルで返す。

```
Player のテスト                          ホスト（android_ui_driver.py）
  Debug.Log("NTK-UI|7|tap-res|android:id/button1")
        ── logcat ──────────────────────▶  依頼を拾う
                                           uiautomator dump → 部品を探す → input tap
                                           結果を端末のファイルに書く
  ファイル <files>/ntk-ui/7 を待つ  ◀──── adb shell で書く
  結果（ok / fail: 理由）を読む
  イベントを待って値を照らす
```

`scripts/verify_unity_android.sh` が、実機の PlayMode を流す前に補助を起動し、終わったら止める。補助がいない実行（Unity の Test Runner の窓から流すなど）では、依頼の結果のファイルが来ないので、UI を使うテストは期限で `Assert.Ignore` にする（失敗にしない。補助のない実行を赤にしないため）。

## 2. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| Appium + UiAutomator2 | Node と Appium の導入と版の固定が要る。この用途（押す、入れる、読む）には過ぎる。クリップボードの層 3 で既定 IME を変える副作用もある（`testing.md` 2 節）。今回は使わない |
| アプリの中から操作する（`Instrumentation` で自分の窓へキーを送る、Java の反射でボタンを押す） | 自分のプロセスの窓にしか届かない。Share の選択画面と通知のシェードはシステムの UI で、届かない。Android 9 以降は隠し API の反射も止められる |
| instrumentation のテスト APK（UiAutomator）を別に入れる | Unity の Test Runner の Player と別の APK を組み、同時に走らせる仕組みが要る。adb の `uiautomator dump` と `input` で同じ部品に届く |

`uiautomator dump` は UiAutomator と同じアクセシビリティの木を XML で出す。native-toolkit のテスト（`android_library_capi_test` の `UiDriver.kt`、サンプルの `ViewDialogs.kt`）が使う選び方（`resource-id`、`text`、`class`、`checked`）は、そのまま XML の属性で使える。

## 3. 依頼の形

テストは `Debug.Log` で 1 行を出す。Unity はこれを logcat のタグ `Unity` に出す。

```
NTK-UI|<番号>|<命令>|<引数 1>|<引数 2>...
```

- `<番号>` はテストの中で増やす整数。補助は結果をこの番号のファイルに書く
- 引数の文字列は Base64 の UTF-8（`|` と非 ASCII と空白を運ぶため）。命令の表で「生」と書いたものはそのまま
- 補助は、起動より前の行を読まない（起動時に `adb logcat -c` で消し、`-T` で起動の時刻から読む）

最初に 1 回、テストの準備が出す:

```
NTK-UI-HELLO|<パッケージ名>|<結果のフォルダ>
```

結果のフォルダは、アプリの外部の領域 `getExternalFilesDir(null)/ntk-ui`（`Application.persistentDataPath` の下）。`adb shell` はここに書ける（`/sdcard/Android/data/<パッケージ>/files`。**要検証**: Android 16 でシェルが書けるか。書けなければ `run-as <パッケージ>` で内部の領域に書く。テスト用の Player は development ビルドで debuggable なので `run-as` が使える）。

### 3.1 命令

| 命令 | 引数 | すること |
|---|---|---|
| `wait` | `<選び方>` | 部品が出るまで木を読み直す（既定 10 秒） |
| `wait-gone` | `<選び方>` | 部品が消えるまで待つ |
| `tap` | `<選び方>` | 部品の `bounds` の中心を `input tap`。押す前に部品が `enabled="true"` であることを確かめる（無効なら `fail: disabled`） |
| `type` | `<選び方>`、`<文字列>` | 部品を押してから `input text`。**ASCII だけ**（`input text` が非 ASCII を運べない）。空白は `%s` にする |
| `back` | なし | `input keyevent KEYCODE_BACK` |
| `expect` | `<選び方>`、`<属性>`、`<値>` | 部品の属性（`text`、`enabled`、`checked`、`password`）が値と同じか。`text` は大文字と小文字を区別する |
| `count` | `<選び方>` | 合う部品の数を結果に書く |

`<選び方>` は `属性=値` を `&` でつないだもの（生。値は Base64）。属性は `resource-id`、`text`、`class`、`package`、`index`（同じ選び方に合う部品のうち、上から何番目か。0 から）。例: `class=YW5kcm9pZC53aWRnZXQuRWRpdFRleHQ=&index=1`（2 つ目の `EditText`）。

### 3.2 結果

`<結果のフォルダ>/<番号>` に 1 行: `ok`、`ok <値>`（`count`）、`fail <理由>`。テストは 15 秒待ち、来なければ「補助がいない」として `Assert.Ignore`。`fail` は `Assert.Fail`。

## 4. 端末の準備（補助が起動のときに行う）

- 画面: `input keyevent KEYCODE_WAKEUP`、`wm dismiss-keyguard`。`dumpsys window` でまだロックされていれば（PIN のある端末）、止めて理由を出す
- アニメーション: `settings put global window_animation_scale 0` と `transition_animation_scale`、`animator_duration_scale` を 0 にし、終了のときに元の値に戻す（押す前に部品が動くのを防ぐ。native-toolkit の `UiDriver.kt` は代わりに `waitForIdle` を使う）。**戻せなかったら補助の終了コードを 1 にし、スクリプトを失敗にする**（`testing.md` の IME の扱いと同じ考え方）
- 画面の向き: 触らない（Dialog の回転は手動。Dialog 設計 v1 の 7.4）

## 5. テストの側（`Tests/PlayMode/AndroidUiDriver.cs`）

- `internal static class AndroidUiDriver`（`#if UNITY_ANDROID`）。`IEnumerator Do(string command, params string[] args)` を `yield return` で待ち、結果を `LastResult` に置く
- `HELLO` は最初の `Do` の前に 1 回だけ出す
- 依頼を出したら、結果のファイルを毎フレーム見る。来たら消す
- Unity のテストのスレッドは UnityMain なので、待つ間も dispatcher は回り、ネイティブの完了のイベントは届く

## 6. 補助の実装（`scripts/android_ui_driver.py`）

- 依存: Python 3 の標準ライブラリ（`subprocess`、`xml.etree`、`base64`）と、Unity 同梱の adb（`verify_unity_android.sh` が `PATH` の先に置く）
- `adb logcat -v raw -s Unity:I -T <時刻>` を 1 行ずつ読み、`NTK-UI` で始まる行だけを扱う
- 木の読み方: `adb exec-out uiautomator dump /dev/tty` は端末によって余計な行が付くので、`uiautomator dump /sdcard/ntk-ui-dump.xml` と `adb exec-out cat` で読む
- 1 つの命令は 10 秒で打ち切り、`fail timeout` を書く
- 終了: `verify_unity_android.sh` が `SIGTERM` を送る。補助は 4 章の設定を戻してから終わる

## 7. テスト

- 補助そのものの確かめ方: Dialog の層 2b が最初の利用者で、そこで通ることをもって確かめる（補助だけのテストは作らない）
- 依頼の行の組み立て（Base64、選び方）は C# の側の純粋な関数にし、層 1（EditMode）で固める
- 補助の選び方の照合は、Python の `doctest` で XML の小さな例に当てる（`python3 -m doctest scripts/android_ui_driver.py`）

## 8. 要検証

| # | 内容 | 確かめる時点 |
|---|---|---|
| V-1 | Android 16 で `adb shell` が `/sdcard/Android/data/<パッケージ>/files` に書けるか（だめなら `run-as`） | 実装の最初 |
| V-2 | `uiautomator dump` が、透明な宿主や Unity の窓の上のダイアログを出すか（`android.app.AlertDialog` の部品が `resource-id` つきで出るか） | Dialog の層 2b の最初 |
| V-3 | ボタンの文字が大文字に変えられて木に出ないか（native-toolkit の `UiDriver.kt` は大文字と小文字を区別しない照合にしていた） | 同上。出るなら `expect` の `text` に大文字と小文字を区別しない形を足す |
| V-4 | Unity の `Debug.Log` の行が、分かれずに 1 行で logcat に出るか（長い Base64） | 実装の最初 |
