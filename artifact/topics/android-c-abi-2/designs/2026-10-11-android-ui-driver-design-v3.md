# Android の Player テストから OS の画面を操作する補助（ホストの adb）v3

## 基本情報

| 項目 | 内容 |
|---|---|
| 対象 | Android の層 2b（実機の Player の PlayMode テスト）で、ダイアログ・Share の選択画面・通知の許可とシェードを押す、読む |
| 最初の利用者 | Dialog（`artifact/android/dialog/designs/2026-10-11-android-dialog-design-v3.md`。Dialog 設計 v3 7.3、8.3） |
| 後の利用者 | Share、Notification（親課題の「進め方」の段 3） |
| 方針の出典 | `agent-rules/coding-rules/testing.md` 2 節（Android は「Appium + UiAutomator2、または UiAutomator」）。Windows は同じ節の考え方で、Appium ではなく PowerShell の Win32 メッセージを選んだ（`artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md`） |
| レビュー | `artifact/android/dialog/reviews/2026-10-11-android-dialog-design-review-v1.md`、`...-review-v2.md` |

## v2 からの主な変更

- ゲート（R2-M-2、Codex v1 の 3）: 補助は `READY` を書けなかったとき、`HELLO` から `READY` まで 5 秒を超えたとき、終了コードを 0 以外にする。補助は `run_tests` の 1 回ごとに起動・停止する。結果の XML でカテゴリ `AndroidUiDriver` のテストの skip が 0 であることを判定に入れる（7 章）
- ユーザー（Codex v2 の 2）: `HELLO` に Android のユーザー ID を入れ、補助は `run-as <package> --user <id>` で書く。`READY` の前に、補助が書いたものをテストが読んで消す往復を 1 回行う
- 画面の点灯（Codex v2 の 3、R2-M-6）: `stay_on_while_plugged_in` の値を覚えて戻す。`svc power stayon` は使わない
- `rotate`（Codex v2 の 4）: 完了の条件を「今の向きが指定した向きと同じ」にする。向きを読む `rotation` を足した。テストは今と違う向きを選ぶ
- `background`（R2-M-3）: 後ろに回し、止まったことを確かめてから結果を書き、時間を置いて前に戻す命令を足した
- 細部（R2-M-6）: `tap-outside` を部品の `bounds` から計算する。`back` は `wait` と同じ期限で待つ。新しい `<run>` の `HELLO` で切り替える。時刻は端末で取る。`fail` の理由は英数字と `-` だけ
- `run-as` が使えないとき（R2-M-4）: V-1 に debuggable の確認を足し、代わりの手段（`adb reverse` のソケット）を書いた

## v1 からの主な変更（v2）

- 補助が応答したかで分けた（`HELLO` と `READY`）。実行ごとの `<run>`、一時ファイルと名前の変更。`run-as` とアプリの内部の領域に一本化。`back` に選び方。依頼の行の長さの上限。`type` の文字の制限。設定を戻す経路。層 1 のテストをやめた

## 1. 何を作るか

ホストの Python のスクリプト `scripts/android_ui_driver.py` が、テストの実行の間 `adb logcat` を読み、テストからの依頼の行に応じて `adb shell uiautomator dump` と `adb shell input` で画面を操作し、結果を端末のファイルで返す。

```
Player のテスト                                    ホスト（android_ui_driver.py）
  Debug.Log("NTK-UI-HELLO|<run>|<package>|<user>")
        ── logcat ────────────────────────────▶   run を切り替える。<files>/ntk-ui/<run>/ を作り直す
                                                   probe を書く
  probe を読んで消す                    ◀──────
                                                   probe が消えたのを見て ready を書く
  <files>/ntk-ui/<run>/ready を待つ     ◀──────
  Debug.Log("NTK-UI|<run>|7|tap|...")
        ── logcat ────────────────────────────▶   uiautomator dump → 部品を探す → input tap
  <files>/ntk-ui/<run>/7 を待つ         ◀──────   .7.tmp に書いて 7 に名前を変える
  結果（ok / fail 理由）を読む
  イベントを待って値を照らす
```

`<files>` はアプリの内部の領域（`Context.getFilesDir()`）。補助は `adb shell run-as <package> --user <user>` でここに書く。テスト用の Player は development ビルドで debuggable なので `run-as` が使える（V-1 で確かめる）。

`scripts/verify_unity_android.sh` が、実機の PlayMode の実行の 1 回ごとに補助を起動し、終わったら止める。

## 2. 選ばなかった案

| 案 | 選ばなかった理由 |
|---|---|
| Appium + UiAutomator2 | Node と Appium の導入と版の固定が要る。この用途（押す、入れる、読む）には過ぎる。クリップボードの層 3 で既定 IME を変える副作用もある（`testing.md` 2 節） |
| アプリの中から操作する（`Instrumentation` で自分の窓へキーを送る、Java の反射でボタンを押す） | 自分のプロセスの窓にしか届かない。Share の選択画面と通知のシェードはシステムの UI で、届かない |
| instrumentation のテスト APK（UiAutomator）を別に入れる | Unity の Test Runner の Player と別の APK を組み、同時に走らせる仕組みが要る。adb の `uiautomator dump` と `input` で同じ部品に届く |
| 結果を外部の領域（`/sdcard/Android/data/<package>/files`）で返す | Android 11 以降の保存の制限で、シェルが書けるかが端末と版に左右される。`run-as` は debuggable なら確実 |

**`run-as` が使えない端末のための代わりの手段**（V-1 が通らなかったときに切り替える。今は作らない）: 補助が `adb reverse tcp:<port> tcp:<port>` を張ってホストで待ち、テストが `127.0.0.1:<port>` に依頼を送り、同じ接続で結果を受け取る。logcat も保存領域も使わない。

`uiautomator dump` は UiAutomator と同じアクセシビリティの木を XML で出す。native-toolkit のテスト（`android_library_capi_test` の `UiDriver.kt`、サンプルの `ViewDialogs.kt`）が使う選び方（`resource-id`、`text`、`class`、`checked`）は、そのまま XML の属性で使える。

## 3. 依頼の形

テストは `Debug.Log` で 1 行を出す。Unity はこれを logcat のタグ `Unity` に出す。

```
NTK-UI-HELLO|<run>|<パッケージ名>|<ユーザー ID>
NTK-UI|<run>|<番号>|<命令>|<引数 1>|<引数 2>...
```

- `<run>` はテストのプロセスが最初に 1 回作る GUID（32 桁の 16 進）。補助は新しい `<run>` の `HELLO` を受けたら、それに切り替え、前の `<run>` のフォルダを消す。今の `<run>` 以外の依頼の行は捨てる
- `<ユーザー ID>` は `<files>` のパス（`/data/user/<id>/<package>/files`）から取る
- `<番号>` は `<run>` の中で 1 から増やす整数（テストの間で戻さない）
- 文字列の引数は Base64 の UTF-8。命令の表で「生」と書いたものはそのまま
- **1 行は UTF-8 で 1000 バイトまで。** テストの側で組み立てるときに超えたら `ArgumentException`（logcat の 1 件は 4076 バイトで切れる。Unity の前置きと余裕を見て 1000 にした）
- 補助は、起動より前の行を読まない（起動時に `adb logcat -c` で消し、端末の時計で取った起動の時刻 `adb shell date +%m-%d\ %H:%M:%S.000` を `-T` に渡す）

### 3.1 命令

| 命令 | 引数 | すること |
|---|---|---|
| `wait` | `<選び方>` | 部品が出るまで木を読み直す（期限 10 秒） |
| `wait-gone` | `<選び方>` | 部品が消えるまで待つ（同じ期限） |
| `tap` | `<選び方>` | 部品が出るまで待ち、`bounds` の中心を `input tap`。押す前に部品が `enabled="true"` であることを確かめる（無効なら `fail disabled`） |
| `tap-outside` | `<選び方>` | 部品が出るまで待ち、画面の上端と部品の上端の中点（横は画面の中央）を `input tap`。その間が 48 ピクセルに満たなければ `fail no-outside` |
| `type` | `<選び方>`、`<文字列>`（生） | 部品を押してから `input text`。**文字列は `[a-z0-9]` の 1〜64 文字だけ**（範囲外は `fail bad-text`）。シェルの escape と `%s` の変換、IME の自動修正・大文字化を避けるため |
| `back` | `<選び方>` | 部品が出るまで待つ（`wait` と同じ期限）。入力のキーボードが出ていれば（`dumpsys input_method` の `mInputShown=true`）、先に 1 回 Back で閉じ、閉じたことを確かめる。そのあと `input keyevent KEYCODE_BACK` |
| `expect` | `<選び方>`、`<属性>`（生）、`<値>` | 部品が出るまで待ち、属性（`text`、`enabled`、`checked`、`password`）が値と同じか。`text` は大文字と小文字を区別する |
| `count` | `<選び方>` | 合う部品の数を結果に書く（待たない） |
| `rotation` | なし | 今の向き（`dumpsys display` の表示の回転、0〜3）を結果に書く |
| `rotate` | `0` / `1` / `2` / `3`（生） | `settings put system accelerometer_rotation 0` と `user_rotation <値>`。**今の向きが指定した値と同じになるまで**待つ（期限 10 秒） |
| `background` | `<ミリ秒>`（生。100〜10000） | `input keyevent KEYCODE_HOME`。アプリの Activity が止まった（`dumpsys activity activities` でアプリの Activity が `RESUMED` でない）ことを確かめてから、結果の `ok` を書く。そのあと指定の時間待ち、`am start` で `<パッケージ名>` の起動の Activity を前に戻す。戻ったこと（`RESUMED`）は確かめるが、結果はもう書いてあるので、戻せなかったときは補助の終了コードを 1 にする |
| `home` | なし | `input keyevent KEYCODE_HOME` |
| `front` | なし | `am start` で `<パッケージ名>` の起動の Activity を前に戻す（`monkey` は使わない。別の Activity を起こすことがある） |

`<選び方>` は `属性=値` を `&` でつないだもの（生。値は Base64）。属性は `resource-id`、`text`、`class`、`package`、`index`（同じ選び方に合う部品のうち、上から何番目か。0 から）。例: `class=YW5kcm9pZC53aWRnZXQuRWRpdFRleHQ=&index=1`（2 つ目の `EditText`）。

### 3.2 結果

`<files>/ntk-ui/<run>/<番号>` に 1 行: `ok`、`ok <値>`（`count`、`rotation`）、`fail <理由>`。理由は英数字と `-` だけ（`run-as … sh -c '…'` の中で引用を要らなくするため）。
補助は `<番号>.tmp` に書いてから `mv` で `<番号>` にする（同じフォルダの中の名前の変更。どちらも `run-as` の中で行うので、所有者はアプリ）。

テストの待ち方:

| 状況 | テスト |
|---|---|
| `HELLO` の後、`ready` が 5 秒来ない | 補助がいないとみなし、そのテストを `Assert.Ignore`。以後の `Do` も Ignore |
| `ready` が来た後、結果が 20 秒来ない | `Assert.Fail("ui driver did not answer")` |
| `fail ...` | `Assert.Fail` |

20 秒は、補助の 1 命令の期限 10 秒と、`uiautomator dump`（1〜3 秒）と `run-as` の往復の余裕。`background` は指定の時間に関わらず、止まったことを確かめた時点で結果を書くので同じ期限で足りる。

## 4. 端末の準備と後片付け

起動のとき（補助）:

- 画面: `input keyevent KEYCODE_WAKEUP`、`wm dismiss-keyguard`。`dumpsys window` でまだロックされていれば（PIN のある端末）、理由を出して終了コード 2 で止める
- 次の設定を、値と「設定があったか」の両方を覚えてから変える（`settings get` が `null` なら「無かった」）:

| 設定 | 変える値 | 変える時点 |
|---|---|---|
| `global stay_on_while_plugged_in` | 7（AC・USB・無線の充電中は点けたまま） | 起動のとき |
| `global window_animation_scale`、`transition_animation_scale`、`animator_duration_scale` | 0 | 起動のとき |
| `system accelerometer_rotation`、`user_rotation` | `rotate` の値 | `rotate` の命令のとき |

終わりのとき（補助の `finally`。`SIGTERM`・`SIGINT`・`SIGHUP` でも通る）:

- 覚えた値に戻す。**無かった設定は `settings delete` で消す**（`put null` は元の状態に戻らない）
- 戻せなかったら終了コード 1

`verify_unity_android.sh` は補助を起動したら `trap ... EXIT INT TERM HUP` で補助に `SIGTERM` を送って待つ。スクリプトが途中で落ちても補助が戻す。

## 5. テストの側（`Tests/PlayMode/AndroidUiDriver.cs`）

- `internal static class AndroidUiDriver`（外 `#if UNITY_ANDROID`、中 `#if !UNITY_EDITOR`）。`IEnumerator Do(string command, params string[] args)` を `yield return` で待ち、結果を `LastResult` に置く
- 最初の `Do` の前に 1 回、`<run>` を作って `HELLO` を出し、probe のファイルが来たら読んで消し、`ready` を待つ
- 結果のファイルは毎フレーム見る。読んだら消す
- `background` の結果だけは、別のスレッドからも待てるように `WaitForResultFile(int number, TimeSpan timeout)`（ファイルを見るだけ。Unity の API に触らない）を用意する。依頼の行と番号は UnityMain で出す
- `<files>` は `UnityPlayer.currentActivity.getFilesDir()`（`AndroidJavaObject`。テストの側だけで使う）
- Unity のテストのスレッドは UnityMain なので、待つ間も取り出し口の `Update` は回り、ネイティブの完了のイベントは届く

## 6. 補助の実装（`scripts/android_ui_driver.py`）

- 依存: Python 3 の標準ライブラリ（`subprocess`、`xml.etree`、`base64`、`signal`）と、Unity 同梱の adb（`verify_unity_android.sh` が `PATH` の先に置く）
- `adb logcat -v raw -s Unity:I -T <時刻>` を 1 行ずつ読み、`NTK-UI` で始まる行だけを扱う
- 木の読み方: `uiautomator dump /data/local/tmp/ntk-ui-dump.xml` の前に古いファイルを消し、`adb exec-out cat` で読む。`could not get idle state` などで失敗したら 1 秒おいて読み直す（期限まで）
- 1 つの命令は 10 秒で打ち切り、`fail timeout` を書く
- 終了のときに、受けた `HELLO` の数、返した `READY` の数、命令の数を標準出力に出す
- 終了コード: 0 は正常。1 は設定を戻せなかった、`background` で前に戻せなかった。2 は端末の準備ができなかった。3 は `HELLO` を受けたのに `READY` を返せなかった（probe の往復の失敗、`run-as` の失敗、5 秒を超えた）

## 7. ゲートでの判定（`verify_unity_android.sh`）

- 実機の PlayMode の実行（`run_tests` の 1 回）ごとに補助を起動し、`trap` を張り、終わったら止める
- 次のどれかなら、その実行を失敗にする:
  - 補助の終了コードが 0 でない
  - 64 ビットの実行で、結果の XML のカテゴリ `AndroidUiDriver` のテストに skip（Ignored）がある（補助に届かなかった UI のテストを緑にしない）
  - 64 ビットの実行で、カテゴリ `AndroidUiDriver` のテストが 1 本も無い（カテゴリの付け忘れ）
- `--mono`（ARMv7）の実行では、64 ビットのテストが Ignore になるので、上の 2 つと 3 つ目は判定から外す（補助の終了コードだけを見る）
- 補助の `doctest` を最初に流す（8 章）

## 8. テスト

- 補助そのものの確かめ方: Dialog の層 2b が最初の利用者で、そこで通ることをもって確かめる
- 補助の選び方の照合、依頼の行の解釈、`tap-outside` の座標の計算、設定の戻し方（値があった・無かった）は、Python の `doctest` で小さな例に当てる（`python3 -m doctest scripts/android_ui_driver.py`）。`verify_unity_android.sh` の最初に流す
- テストの側の行の組み立ては、層 1 では書かない（PlayMode のアセンブリにあり、EditMode のテストから参照できない）。Dialog の層 2b が毎回通る

## 9. 要検証

| # | 内容 | 確かめる時点 |
|---|---|---|
| V-1 | テスト用の Player が debuggable であること（`dumpsys package <package>` の `flags` に `DEBUGGABLE`）。`run-as <package> --user <id> sh -c 'cat > files/...'` で補助が書いたファイルを、アプリが読めて消せること（probe の往復）。通らなければ 2 章の代わりの手段に切り替える | 実装の最初 |
| V-2 | `uiautomator dump` が、Unity の窓の上のダイアログを出すか（`android.app.AlertDialog` の部品が `resource-id` つきで出るか） | Dialog の層 2b の最初 |
| V-3 | ボタンの文字が大文字に変えられて木に出ないか（native-toolkit の `UiDriver.kt` は大文字と小文字を区別しない照合にしていた） | 同上。出るなら `expect` の `text` に大文字と小文字を区別しない形を足す |
| V-4 | Unity の `Debug.Log` の行が、分かれずに 1 行で logcat に出るか（1000 バイトまで） | 実装の最初 |
| V-5 | Unity が毎フレーム描いている間に `uiautomator dump` が失敗し続けないか | V-2 と同時 |
| V-6 | `dumpsys input_method` の `mInputShown` で、Android 16 のキーボードの表示を判定できるか | Dialog の入力のテスト |
| V-7 | `rotate` の後、Unity と透明な宿主の窓が作り直されないこと（`configChanges`） | Dialog の回転のテスト |
| V-8 | `background` で Unity が一時停止している間も、Unity Test Runner と Editor の接続（PlayerConnection）が切れず、前に戻ったあとテストが続くこと | Dialog の E-8 のテスト |
