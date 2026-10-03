# 通知の `Audio.Src` が効かない

- 記録日: 2026-09-27
- 分類: Windows Notification 固有。公開 API の見た目と、実際の動きのずれ
- 発見経緯: C ABI 2.0.0 への移行の設計（`artifact/windows/notification/designs/2026-09-27-windows-notification-design-v8.md`）で、
  公開の JSON の形と、1.x のネイティブが読んでいた形を突き合わせたときに見つけた
- 対応方針: **別タスク。** 移行では 1.x と同じく読まない（設計 v8 の J-5、開発者の判断 2026-09-27）。記録だけ先に残す
- 進捗: 未着手

---

## 何が起きているか

公開の `WindowsNotificationAudioPayload.Src`（例: `"ms-winsoundevent:Notification.Mail"`）は、
`WindowsNotificationJsonBuilder` が JSON の `audio.src` として出す（`WindowsNotificationJsonBuilder.cs` の `BuildAudioObject`）。
**しかし、ネイティブは `src` を読まない。**

- 1.x のネイティブは `audio` の `type`（`"mute"` / `"uri"` / それ以外）、`uri`、`event`、`loop` だけを読んでいた
  （native-toolkit `1.11.0:windows/WindowsLibrary/WindowsNotificationManager.cpp` の `ApplyAudio`）
- 2.0.0 への移行後も、C# が 1.x と同じ規則で JSON を読むので、`src` は読まない（`WindowsNotificationCApi` の `ContentPlanner.Audio`）

結果として、`Src` に何を入れても、通知は既定の音で鳴る。利用者は XML コメントを見て `Src` を設定できるが、効かない。

## 今の手当て

- `WindowsNotificationAudioPayload.Src` の XML コメントに「ネイティブは読まない。何を入れても既定の音」と書いた（2026-09-27）
- 層 1 のテスト（`KeysThat1xDidNotRead_AreNotChecked`）が、`src` を読まないこと（型が違っても誤りにしない）を固定している

## 案

| # | 内容 | 影響 |
|---|---|---|
| 1 | `Src` を読んで効かせる。`ms-winsoundevent:` なら `audio.event`（`kind = EVENT` と名前）へ、それ以外なら `audio.uri`（`kind = URI`）へ振り分ける | 振る舞いが変わる（今は既定の音）。`event` の名前の対応（`reminder` / `alarm` など）と `ms-winsoundevent:` の名前の対応を決める必要がある |
| 2 | `Src` を廃止予定（`[Obsolete]`）にし、`Type` / `Event` / `Uri` を公開の型に足す | 公開 API が増える。JSON の形はネイティブが読む形に揃う |
| 3 | 今のまま（XML コメントだけ） | 変化なし |

**推奨は 2。** JSON の形がネイティブの読む形（`type` / `event` / `uri`）とそろい、1 のような名前の対応表を C# で持たずに済む。
いずれも公開 API か振る舞いの変更なので、次の版の設計（design-feature）で扱う。

## 参照

- 設計: `artifact/windows/notification/designs/2026-09-27-windows-notification-design-v8.md`（J-5、4.2 の表の `audio.src`、2 章の「既知の奇妙な点」）
- 1.x の読み方: native-toolkit タグ `1.11.0` の `windows/WindowsLibrary/WindowsNotificationManager.cpp`（`ApplyAudio`）
