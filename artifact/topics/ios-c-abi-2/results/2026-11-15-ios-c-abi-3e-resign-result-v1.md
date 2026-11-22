# 段 3-E: Release のアーカイブで 2 つの framework が署名し直されることの確かめ v1

- 記録日: 2026-11-15（20:35〜21:00）
- 親: [../README.md](../README.md) の段 3-E
- 時点: `8bbf2b0`（4 機能を 2.0.0 に移した後）。Unity 6000.4.2f1、Xcode 26.3
- 手本: native-toolkit の `artifact/topics/ios-c-abi/results/2026-11-14-ios-c-abi-distribution-signing-procedure.md` の 3 章（D-18）と 6 章の記録

## 1. 何を確かめたか

パッケージが持つ `ios-native-toolkit-2.0.0.xcframework`（`IosLibrary`）と `ios-native-toolkit-capi-2.0.0.xcframework`（`IosLibraryCApi`）は、署名なしで配られる。Unity が書き出した Xcode のプロジェクトで Release のアーカイブを作ると、2 つの framework がアプリに埋め込まれ、アプリと同じ証明書で署名し直されるかを確かめた。

- 配布用の書き出し（Ad Hoc・App Store）と Validate App はしない。利用者のチームは Personal Team で、Apple Developer Program に登録していない（native-toolkit の 2b と同じ。F-17）
- 端末は使わない

## 2. やり方

1 回限りのスクリプトを scratchpad で流した（リポジトリには入れていない）。

1. UI のハーネス（`Assets/NativeToolkitIosUiHarness`。4 機能をパッケージの公開の API で呼ぶアプリ）を、`IosUiHarnessBuild.Export -ntkHarnessSdk device` で実機向けに書き出す。Unity が書き換えるファイルは、`scripts/ios_script_common.sh` の記録と戻しで元に戻す
2. `xcodebuild archive -configuration Release -destination generic/platform=iOS`。署名は自動。チームは `select_team`（期限内のプロファイルのチームが 1 つ）で選び、出力しない
3. アーカイブの中の `Products/Applications/*.app` と、その `Frameworks/` の各 framework について、次を流す:
   - `codesign -dvv` の `Authority=` の行の頭（証明書の種類まで。名前は出さない）
   - `TeamIdentifier=` の行の `shasum` の頭 8 文字（同じか違うかだけを見る）
   - `codesign --verify --deep --strict --verbose=2` の最後の 2 行
4. 比べるため、パッケージの中の 2 つの framework（`ios-arm64`）の署名の有無も見る
5. 書き出し先とアーカイブは、一時フォルダーごと消した

## 3. 結果

| もの | 署名 |
|---|---|
| パッケージの中の `IosLibrary.framework`・`IosLibraryCApi.framework` | `not signed at all`（署名なしで配られている） |
| アーカイブの `UnityNativeToolkit.app` | `Authority=Apple Development:`、team の要約 A |
| 〃 `Frameworks/IosLibrary.framework` | `Authority=Apple Development:`、team の要約 A |
| 〃 `Frameworks/IosLibraryCApi.framework` | `Authority=Apple Development:`、team の要約 A |
| 〃 `Frameworks/UnityFramework.framework`・`UnityRuntime.framework` | `Authority=Apple Development:`、team の要約 A |
| `codesign --verify --deep --strict` | `valid on disk`、`satisfies its Designated Requirement` |

- アーカイブは `** ARCHIVE SUCCEEDED **`
- **署名なしの 2 つの framework は、Unity の `.meta` の Embed で `Frameworks/` に入り、アーカイブのときにアプリと同じ開発用の証明書・同じ team で署名された**
- `Frameworks/` には、この 2 つと Unity の 2 つのほかに何も入っていない（2.0.0 の framework が二重に入ることはない）

## 4. 残ること

- 配布用の証明書（Apple Distribution）での署名し直しと Validate App は、Apple Developer Program の登録が要るので確かめていない（native-toolkit の 2b と同じ）
- git の URL で入れた利用者の形でのアーカイブは、段 4 の I-17（実機）とあわせて見る → 済み（2026-11-22、I-17 の G-5。Personal Team の開発用の証明書で署名し直され、`codesign --verify --deep --strict` が通った。端末は使っていない。[I-17 の結果](2026-11-22-ios-c-abi-i17-git-url-result-v1.md)）

## 5. 途中で起きたこと

- `Authority=` の行を 30 文字で切ったため、証明書の名前の頭の 1 文字が会話の出力に出た（native-toolkit の手順と同じ切り方。手順の想定は Distribution で、`Apple Distribution: ` が 20 文字だった）。ファイルとコミットには書いていない。scratchpad のログからは消した。次からは `Authority=` を `:` までで切る
