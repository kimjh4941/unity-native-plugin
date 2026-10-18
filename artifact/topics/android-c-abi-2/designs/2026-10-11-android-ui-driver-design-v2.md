# Android の Player テストから OS の画面を操作する補助（ホストの adb）v2

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android の層 2b（実機の Player の PlayMode テスト）で、ダイアログ・Share の選択画面・通知の許可とシェードを押す、読む |
| 最初の利用者 | Dialog（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v2.md`。Dialog 設計 v2 7.3、8.3） |
| 後の利用者 | Share、Notification（親課題の「進め方」の段 3） |
| 方針の出典 | `agent-rules/coding-rules/testing.md` 2 節（Android は「Appium + UiAutomator2、または UiAutomator」）。Windows は同じ節の考え方で、Appium ではなく PowerShell の Win32 メッセージを選んだ（`artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`） |
| レビュー | `artifact/android/dialog/reviews/2026-10-11-android-dialog-design-review-v1.md`（M-4、M-5、M-6、Codex 3〜9） |

## v1 からの主な変更

- 補助が応答したかで分ける。`HELLO` に補助が `READY` を返す。`READY` が来なければ（補助がいない）`Assert.Ignore`、`READY` の後の期限切れは `Assert.Fail`。`verify_unity_android.sh` は補助が `HELLO` を受けたことを判定に入れる（M-4、Codex 3）
- 実行ごとの ID（`HELLO` に含める）でフォルダを分け、結果は一時ファイルに書いてから同じフォルダの中で名前を変える（M-5 (1)(2)、Codex 4）
- 結果の受け渡しを `run-as` とアプリの内部の領域に一本化した（Codex 5）。外部の領域を使う案と、その要検証をやめた
- `back` に選び方を要るものにした（Codex 6）。入力のキーボードが出ていれば先に閉じる（M-5 (5)）
- 依頼の行の長さの上限（Codex 7）、`type` の文字の制限（Codex 8、M-5 (6)）
- 端末の設定を戻す経路を、補助の `finally` とシグナル、スクリプトの `trap` の両方にし、元の値が無い設定は消して戻す（Codex 9、M-5 (4)）。画面を点けたままにする（M-5 (3)）。回転の命令を足した（Dialog 設計 v2 の 7.3）
- 依頼の行の組み立ての層 1 のテストをやめた（M-6）。行の解釈は補助の `doctest`、全体は Dialog の層 2b で確かめる

## 1. 何を作るか

ホストの Python のスクリプト `scripts/android_ui_driver.py` が、テストの実行の間 `adb logcat` を読み、テストからの依頼の行に応じて `adb shell uiautomator dump` と `adb shell input` で画面を操作し、結果を端末のファイルで返す。

```
Player のテスト                                  ホスト（android_ui_driver.py）
  Debug.Log("NTK-UI-HELLO|<run>|<package>")
        ── logcat ──────────────────────────▶   run を覚える。<files>/ntk-ui/<run>/ を作り直す
  <files>/ntk-ui/<run>/ready を待つ   ◀────────  READY を書く
  Debug.Log("NTK-UI|<run>|7|tap|...")
        ── logcat ──────────────────────────▶   uiautomator dump → 部品を探す → input tap
  <files>/ntk-ui/<run>/7 を待つ       ◀────────  .7.tmp に書いて 7 に名前を変える
  結果（ok / fail 理由）を読む
  イベントを待って値を照らす
```

`<files>` はアプリの内部の領域（`Context.getFilesDir()`）。補助は `adb shell run-as <package>` でここに書く。テスト用の Player は development ビルドで debuggable なので `run-as` が使える。

`scripts/verify_unity_android.sh` が、実機の PlayMode を流す前に補助を起動し、終わったら止める。

## 2. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| Appium + UiAutomator2 | Node と Appium の導入と版の固定が要る。この用途（押す、入れる、読む）には過ぎる。クリップボードの層 3 で既定 IME を変える副作用もある（`testing.md` 2 節） |
| アプリの中から操作する（`Instrumentation` で自分の窓へキーを送る、Java の反射でボタンを押す） | 自分のプロセスの窓にしか届かない。Share の選択画面と通知のシェードはシステムの UI で、届かない |
| instrumentation のテスト APK（UiAutomator）を別に入れる | Unity の Test Runner の Player と別の APK を組み、同時に走らせる仕組みが要る。adb の `uiautomator dump` と `input` で同じ部品に届く |
| 結果を外部の領域（`/sdcard/Android/data/<package>/files`）で返す | Android 11 以降の保存の制限で、シェルが書けるかが端末と版に左右される。`run-as` は debuggable なら確実 |

`uiautomator dump` は UiAutomator と同じアクセシビリティの木を XML で出す。native-toolkit のテスト（`android_library_capi_test` の `UiDriver.kt`、サンプルの `ViewDialogs.kt`）が使う選び方（`resource-id`、`text`、`class`、`checked`）は、そのまま XML の属性で使える。

## 3. 依頼の形

テストは `Debug.Log` で 1 行を出す。Unity はこれを logcat のタグ `Unity` に出す。

```
NTK-UI-HELLO|<run>|<パッケージ名>
NTK-UI|<run>|<番号>|<命令>|<引数 1>|<引数 2>...
```

- `<run>` はテストのプロセスが最初に 1 回作る GUID（32 桁の 16 進）。補助は今の `<run>` 以外の行を捨てる
- `<番号>` は `<run>` の中で 1 から増やす整数（テストの間で戻さない）
- 文字列の引数は Base64 の UTF-8。命令の表で「生」と書いたものはそのまま
- **1 行は UTF-8 で 1000 バイトまで。** テストの側で組み立てるときに超えたら `ArgumentException`（logcat の 1 件は 4076 バイトで切れる。Unity の前置きと余裕を見て 1000 にした）
- 補助は、起動より前の行を読まない（起動時に `adb logcat -c` で消し、`-T` で起動の時刻から読む）

### 3.1 命令

| 命令 | 引数 | すること |
|---|---|---|
| `wait` | `<選び方>` | 部品が出るまで木を読み直す（既定 10 秒） |
| `wait-gone` | `<選び方>` | 部品が消えるまで待つ |
| `tap` | `<選び方>` | 部品の `bounds` の中心を `input tap`。押す前に部品が `enabled="true"` であることを確かめる（無効なら `fail disabled`） |
| `tap-outside` | `<選び方>` | 部品の `bounds` の外（上の余白の中央、画面の高さの 1/12）を `input tap` |
| `type` | `<選び方>`、`<文字列>`（生） | 部品を押してから `input text`。**文字列は `[a-z0-9]` の 1〜64 文字だけ**（範囲外は `fail bad-text`）。シェルの escape と `%s` の変換、IME の自動修正・大文字化を避けるため |
| `back` | `<選び方>` | 部品が出ていることを確かめてから `input keyevent KEYCODE_BACK`。入力のキーボードが出ていれば（`dumpsys input_method` の `mInputShown=true`）、先に 1 回 Back で閉じ、閉じたことを確かめてから送る |
| `expect` | `<選び方>`、`<属性>`（生）、`<値>` | 部品の属性（`text`、`enabled`、`checked`、`password`）が値と同じか。`text` は大文字と小文字を区別する |
| `count` | `<選び方>` | 合う部品の数を結果に書く |
| `rotate` | `0` / `1` / `2` / `3`（生） | `settings put system accelerometer_rotation 0` と `user_rotation` で向きを変え、`wm` の表示が変わるまで待つ |
| `home` | なし | `input keyevent KEYCODE_HOME` |
| `front` | なし | `am start` で `<パッケージ名>` の起動の Activity を前に戻す（`monkey -p <パッケージ名> 1` は使わない。別の Activity を起こすことがある） |

`<選び方>` は `属性=値` を `&` でつないだもの（生。値は Base64）。属性は `resource-id`、`text`、`class`、`package`、`index`（同じ選び方に合う部品のうち、上から何番目か。0 から）。例: `class=YW5kcm9pZC53aWRnZXQuRWRpdFRleHQ=&index=1`（2 つ目の `EditText`）。

### 3.2 結果

`<files>/ntk-ui/<run>/<番号>` に 1 行: `ok`、`ok <値>`（`count`）、`fail <理由>`。
補助は `<番号>.tmp` に書いてから `mv` で `<番号>` にする（同じフォルダの中の名前の変更なので、テストは書きかけを読まない）。

テストの待ち方:

| 状況 | テスト |
|---|---|
| `HELLO` の後、`ready` が 5 秒来ない | 補助がいないとみなし、そのテストを `Assert.Ignore`。以後の `Do` も Ignore |
| `ready` が来た後、結果が 20 秒来ない | `Assert.Fail("ui driver did not answer")` |
| `fail ...` | `Assert.Fail` |

20 秒は、補助の 1 命令の期限 10 秒と、`uiautomator dump`（1〜3 秒）と `run-as` の往復の余裕。

## 4. 端末の準備と後片付け

起動のとき（補助）:

- 画面: `input keyevent KEYCODE_WAKEUP`、`wm dismiss-keyguard`。`dumpsys window` でまだロックされていれば（PIN のある端末）、理由を出して終了コード 2 で止める
- 点けたまま: `svc power stayon usb`
- アニメーション: `settings get global window_animation_scale`、`transition_animation_scale`、`animator_duration_scale` を覚えてから 0 にする
- 向き: `accelerometer_rotation` と `user_rotation` を覚える（変えるのは `rotate` の命令のときだけ）

終わりのとき（補助の `finally`。`SIGTERM`・`SIGINT`・`SIGHUP` でも通る）:

- 覚えた値に戻す。**`settings get` が `null` を返した設定は `settings delete` で消す**（`put null` は元の状態に戻らない）
- `svc power stayon false`
- 戻せなかったら終了コード 1

`verify_unity_android.sh` は補助を起動したら `trap ... EXIT INT TERM HUP` で補助に `SIGTERM` を送って待つ。スクリプトが途中で落ちても補助が戻す。

## 5. テストの側（`Tests/PlayMode/AndroidUiDriver.cs`）

- `internal static class AndroidUiDriver`（外 `#if UNITY_ANDROID`、中 `#if !UNITY_EDITOR`）。`IEnumerator Do(string command, params string[] args)` を `yield return` で待ち、結果を `LastResult` に置く
- 最初の `Do` の前に 1 回、`<run>` を作って `HELLO` を出し、`ready` を待つ
- 結果のファイルは毎フレーム見る。読んだら消す
- `<files>` は `UnityPlayer.currentActivity.getFilesDir()`（`AndroidJavaObject`。テストの側だけで使う）
- Unity のテストのスレッドは UnityMain なので、待つ間も Manager の `Update` は回り、ネイティブの完了のイベントは届く

## 6. 補助の実装（`scripts/android_ui_driver.py`）

- 依存: Python 3 の標準ライブラリ（`subprocess`、`xml.etree`、`base64`、`signal`）と、Unity 同梱の adb（`verify_unity_android.sh` が `PATH` の先に置く）
- `adb logcat -v raw -s Unity:I -T <時刻>` を 1 行ずつ読み、`NTK-UI` で始まる行だけを扱う
- 木の読み方: `uiautomator dump /data/local/tmp/ntk-ui-dump.xml` の前に古いファイルを消し、`adb exec-out cat` で読む。`could not get idle state` などで失敗したら 1 秒おいて読み直す（期限まで）
- 1 つの命令は 10 秒で打ち切り、`fail timeout` を書く
- 起動のときに受け付けた `HELLO` の数と命令の数を数え、終了のときに標準出力に出す

## 7. ゲートでの判定（`verify_unity_android.sh`）

- 実機の PlayMode の前に補助を起動し、`trap` を張る
- 終わったら補助を止め、補助の終了コードが 0 であることと、**`HELLO` を 1 回以上受けたこと**を判定に入れる（UI を使うテストが 1 本も補助に届かなかった実行を緑にしない）
- `--mono`（ARMv7）の実行では、64 ビットのテストが Ignore になるので、補助への依頼は来なくてよい（判定から外す）

## 8. テスト

- 補助そのものの確かめ方: Dialog の層 2b が最初の利用者で、そこで通ることをもって確かめる
- 補助の選び方の照合と、依頼の行の解釈は、Python の `doctest` で XML と行の小さな例に当てる（`python3 -m doctest scripts/android_ui_driver.py`）。`verify_unity_android.sh` の最初に流す
- テストの側の行の組み立ては、層 1 では書かない（PlayMode のアセンブリにあり、EditMode のテストから参照できない）。Dialog の層 2b が毎回通る

## 9. 要検証

| # | 内容 | 確かめる時点 |
|---|---|---|
| V-1 | `run-as <package> sh -c 'cat > files/...'` で、補助が書いたファイルをアプリが読めるか（所有者がアプリの uid になるか） | 実装の最初 |
| V-2 | `uiautomator dump` が、Unity の窓の上のダイアログを出すか（`android.app.AlertDialog` の部品が `resource-id` つきで出るか） | Dialog の層 2b の最初 |
| V-3 | ボタンの文字が大文字に変えられて木に出ないか（native-toolkit の `UiDriver.kt` は大文字と小文字を区別しない照合にしていた） | 同上。出るなら `expect` の `text` に大文字と小文字を区別しない形を足す |
| V-4 | Unity の `Debug.Log` の行が、分かれずに 1 行で logcat に出るか（1000 バイトまで） | 実装の最初 |
| V-5 | Unity が毎フレーム描いている間に `uiautomator dump` が失敗し続けないか | V-2 と同時 |
| V-6 | `dumpsys input_method` の `mInputShown` で、Android 16 のキーボードの表示を判定できるか | Dialog の入力のテスト |
| V-7 | `rotate` の後、Unity と透明な宿主の窓が作り直されないこと（`configChanges`） | Dialog の回転のテスト |
