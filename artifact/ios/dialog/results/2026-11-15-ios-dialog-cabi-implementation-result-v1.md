# iOS Dialog の C ABI `2.0.0` への移行の実装の記録 v1

- 記録日: 2026-11-15
- 設計: [../designs/2026-11-15-ios-dialog-cabi-design-v2.md](../designs/2026-11-15-ios-dialog-cabi-design-v2.md)（レビュー v1 で 2 人とも A1 が 0）
- 親: [artifact/topics/ios-c-abi-2](../../../topics/ios-c-abi-2/README.md) の段 3（1 つ目の機能）
- コミット: `2464bed`（移行）、`672fc0a`（`test_ios_ui.sh` の bash `3.2` の直し）、`4fe490c`（実装のレビュー v1 の直し）
- 実装のレビュー: [../reviews/2026-11-15-ios-dialog-cabi-implementation-review-v1.md](../reviews/2026-11-15-ios-dialog-cabi-implementation-review-v1.md)

## 1. 作ったもの

| もの | 中身 |
|---|---|
| `Runtime/iOS/Dialog/IosDialogCApi.cs`（新） | `Dialog.h` の 6 つの要求の構造体（`Pack = 8`）、使う 11 個の関数の宣言（`!UNITY_EDITOR` の外）、C# の確かめ（題・本文・選択肢、**選択肢に null の要素**）、要求の組み立て（1.x の既定を埋める、空のときの旗の反転）、完了からイベントの引数への変換と文言、結果の読み取り（`UNITY_IOS && !UNITY_EDITOR`） |
| `Runtime/iOS/Dialog/IosDialogManager.cs` | 3-S の仮の経路を消し、`ntk_dialog_show_*_async` を呼ぶ。`user_data` は種類、`release` と `out_request_id` は NULL。完了は主スレッドで読んで handle を解放し、次の `Update` にイベントを出す（読めなければ `UNKNOWN`）。購読者の例外は 1.x と同じ文言で `LogError`（呼び出しの中で出すものも）。Editor では `LogWarning` と、呼び出しの中で失敗を 1 回。入口の例外は `Internal error: …`（1.x と Android と同じ） |
| `Runtime/iOS/Common/IosNativeToolkitCApi.cs` | UTF-8 の確保・配列・解放・長さつきの読み取り |
| テスト | EditMode: `IosDialogCApiTests`（構造体の大きさと全項目の位置、組み立て、確かめ、変換と文言）、`IosNativeToolkitCApiStringTests`、`IosCApiDeclarationTests`（期待の 12 個と、**各宣言と完了の delegate の署名**）、`UnityMessageNameTests`（パッケージの MonoBehaviour が Unity のメッセージ名で引数を持つメソッドを宣言しない）。Editor の PlayMode: `IosDialogManagerEditorTests`（Editor の経路の 6 つ、null の要素、購読者の例外〈呼び出しの中と、dispatcher の次の積み荷〉）。Player: ID-22b、`IosDialogCApiPlayerTests`（`struct_size` 0 の要求は `INVALID_PARAMETER` で完了が来ない、完了は次の `Update` に 1 回、結果の無い `NONE` は `UNKNOWN`）。Dialog の `UnavailableUntilMoved` は消した。`NTK_IOS_CABI_2_DIALOG`（参照するテストは今は無いが、後の機能と同じ形のため残す） |
| UI のテスト | `DialogUITests` を `notMovedYet` から外した。ID-17 は `[abi@event] 2` のとき `2.0.0` の期待（出て押せる）。ID-12 はパスワードの全文が合うか（`password-match=True`。値は出さない） |
| 文書 | `agent-rules/coding-rules/common.md`（iOS の C ABI の決まり）、`testing.md`（ガードの表、層 1 の表） |

## 2. 確かめたこと

### 2.1 シミュレーター（iPhone 16 / iOS 18.3、2026-11-15）

`4fe490c`（レビューの直しの後）の結果。`2464bed` でも V-1〜V-3・V-5 は成功していた（EditMode 1485、Editor の PlayMode 304、Player 14、UI 18）。

| 確かめ | 結果 |
|---|---|
| V-1 | `iOS Profile`: EditMode 1488 件（成功 1487、既にあった 1 件の skip）、Editor の PlayMode 306 件成功。`Android™ Profile` の EditMode も 1488 件（成功 1487）で、`IosCApiDeclarationTests` を含む。副作用なし |
| V-2 | Player のテスト 17 件成功（ID-20〜22、ID-22b、C ABI の拒みと完了の経路 3 件、`VersionIsTwo`、delegate、Share・Clipboard・Notification の `UnavailableUntilMoved`）。`iOS Profile` と `--as-package-user` の両方。ログに `Start() can not take parameters` は出ない |
| V-3 | `DialogUITests` 18 件すべて成功。ID 1〜16 は 1.x の記録と全文で同じ行（ID-12 は `password-match=True` を足した行）。**ID-17 は既知の差分のとおり、前面で非活性のときもアラートが出て `[dialog@ok] button=OK`** |
| V-5 | `test_ios_ui.sh` の既定の一覧は `DialogUITests` の 18 件だけ |

### 2.2 実機（iPhone XS / iOS 18.7.2）

利用者の指示（「実機も使っていいので、自動で進めて」）に従い、`--dry-run` の 4 点と承認 ID を会話に書いてから流した。端末の識別子、ログと結果の束は残していない。

| 確かめ | 結果 |
|---|---|
| V-4 | `DialogUITests` の実機の 13 件（ID 1〜11、14a、14b）がすべて成功。`2464bed` と `4fe490c` の 2 回とも |
| V-2（実機） | Player のテスト: `2464bed` で 14 件、`4fe490c` で 17 件がすべて成功 |
| V-6 | サンプルの画面の手の確かめは、利用者に頼む項目として残す |

## 3. 途中で起きたこと

- 実装のレビュー v1 で、Manager の補助のメソッドを `Start` と名付けたため、Unity が読み込みのたびに `Script error (IosDialogManager): Start() can not take parameters.` を出していたことが分かった（Editor の EditMode・書き出しのログ、シミュレーターの Player の起動のログ）。テストは `LogAssert` の外なので捕まえていなかった。`ShowNative` に名前を変え、`UnityMessageNameTests` を足した（`4fe490c`）。直した後の読み込みのログでは 0 件（直す前は 4 件）
- 実機の UI テストの承認（`test_ios_ui.sh --device --dry-run`）が、`--keep-installed` の無い形で `KEEP_FLAG[@]: unbound variable` で止まった。macOS の bash `3.2` は `set -u` のとき空の配列の `"${a[@]}"` を未定義とみなす。`--keep-installed` を足した `5e57387` から、入れ直す形の実機の回は始められなかった（その後の実機の回はどれも `--keep-installed` か Player のテストだった）。空になりうる配列（`KEEP_FLAG`・`INSTALLED_IDS`・`rest`・`SIGNING`）を `${a[@]+"${a[@]}"}` にした（`672fc0a`）

## 4. 設計との違いと既知の差分の足し

- 呼び出しの中で出すイベント（C# の確かめ、Editor、拒まれたとき）でも、購読者の例外は `LogError` になり、`Show*` の呼び出し元へは伝わらない。1.x では伝わった（実装のレビュー v1 の R-2。改善として残し、既知の差分にする）
- Editor の経路は `LogWarning` を出す（1.x は `EntryPointNotFound` の `LogError`。Android の (a) に合わせた）
- 設計 4 章の「ID-16 は `password-match` を空と比べる」は、長さ 0 で空と決まるので `password-length=0` だけにした。`ReadUtf8(IntPtr, UIntPtr)` は `ReadUtf8OrNull` の名前にした。D-7 の購読者の例外のテストは、Manager が `#if UNITY_IOS` なので EditMode ではなく Editor の PlayMode に置いた

## 5. 残っていること

- V-6: サンプルの画面（`IosDialogManagerExampleController`）を実機で手で一度見る（利用者に頼む）
  - 2026-11-22 の追記: 実機で手で見た記録は無い（別に記録していない）。サンプルの画面は自動のテスト（Dialog の 11 件、`e34c6fe`〜`44349ce`）と配置の確かめ（`01a1545`、`a75d802`）で見る形にした。`a75d802` では Dialog のボタンの文字の切れを直した
- iPad のシートの基点（設計 v2 の D-14。段 4 で iPad のシミュレーターの回を足すかを決める）→ 確かめずに出す（利用者の判断、2026-11-21）。iPad のシミュレーターの回は足していない。マニュアル 1.14.0 の `dialog.md` に「Not checked on an iPad」と書いた
