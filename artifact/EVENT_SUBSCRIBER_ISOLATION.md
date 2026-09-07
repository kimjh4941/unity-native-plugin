# イベント購読者が互いから隔離されていない（横断課題）

- 記録日: 2026-09-07
- 分類: 横断課題（Windows Clipboard 固有ではない。Android / iOS / macOS / Windows の全 Manager に共通）
- 発見経緯: Windows Clipboard の実装レビュー v2（A-14）。遅延レンダリング担当のレビュアーが指摘し、
  Windows 固有の退行ではなく既存 Manager と共通の慣行であることを同時に確認した
- 対応方針: **Windows Clipboard の実装には含めない。別途対応。**
  ここで直すと 4 プラットフォームのファイルに手が入り、`common.md` の P2
  （他プラットフォームの既存ファイルを変更しない）に触れる
- 進捗: 未着手

---

## 何が問題か

各 Manager は共通イベントを multicast delegate として 1 回で呼び、
try/catch は**呼び出し全体**を包んでいる。

```csharp
try
{
    common?.Invoke(result);      // 購読者 A → 購読者 B → 購読者 C
}
catch (Exception ex)
{
    Debug.LogError(...);
}
```

購読者 A が例外を投げると、B と C には**その回だけでなく以後毎回**届かない。
A の購読が解除されるまで恒久的に続く。

`GetInvocationList` を使って 1 件ずつ呼び分けている箇所は、パッケージ全体で 0 件。

## 何が問題でないか

誤解しやすい点を明記する。

- **dispatcher は壊れない。** `UnityMainThreadDispatcher` に enqueue されるアクション本体が
  必ず try/catch の内側にあるため、購読者の例外がキュー処理全体を止めることはない
- **共通イベントと per-call callback は隔離されている。** `InvokeInOrder` が 2 者を別々の
  try/catch で呼ぶので、片方の例外がもう片方の結果を潰すことはない
- 隔離されていないのは**共通イベントの購読者どうし**だけである

## 影響範囲

`InvokeInOrder` 相当と、通知系イベントの発火箇所。下記「検査コマンド」で実測した（2026-09-07）。

`GetInvocationList` は Runtime 全体で **0 件**。つまり隔離している箇所はどこにも無い。

| 領域 | 該当ファイル |
|---|---|
| Clipboard | `AndroidClipboardManager` / `IosClipboardManager` / `MacClipboardManager` / `WindowsClipboardManager`（`InvokeInOrder`） |
| Share | `AndroidShareManager` / `IosShareManager` / `MacShareManager` / `ShareChooserActionCallbackCoordinator` |
| Dialog | `AndroidDialogManager` / `IosDialogManager` / `MacDialogManager` / `WindowsDialogManager` |
| Notification | `AndroidNotificationManager` / `IosNotificationManager` / `MacNotificationManager` / `WindowsNotificationManager` |

**4 機能 × 4 プラットフォームすべてが同じ形**であり、Clipboard 固有でも Windows 固有でもない。

## 検査コマンド

```bash
# 隔離している箇所（現状 0 件のはず）
grep -rn "GetInvocationList" Packages/com.jonghyunkim.nativetoolkit/Runtime/

# multicast をまとめて呼んでいる箇所
grep -rn "?.Invoke(" Packages/com.jonghyunkim.nativetoolkit/Runtime/
```

## 対応案

### 案 1: invocation list を 1 件ずつ呼ぶ

```csharp
if (common != null)
{
    foreach (Delegate handler in common.GetInvocationList())
    {
        try { ((Action<TResult>)handler)(result); }
        catch (Exception ex) { Debug.LogError($"... subscriber threw: {ex.Message}"); }
    }
}
```

- 利点: 1 人の購読者の例外が他に波及しない
- 欠点: 発火ごとに `GetInvocationList` が配列を確保する。
  `ClipboardChanged` のように頻度が読めないイベントでは割り当てが増える

### 案 2: 現状を維持し、契約として明記する

「購読者は例外を投げてはならない」を XML ドキュメントに書き、隔離しない理由を残す。

- 利点: コスト 0、割り当てが増えない
- 欠点: 利用者側のバグが他の購読者の機能を静かに壊す。原因の特定が難しい

### 判断に必要なこと

**どちらを採るにせよ 4 プラットフォーム同時に決めること。** 片方だけ直すと挙動が食い違い、
「macOS では届くが Windows では届かない」という種類の差になる。

割り当てコストが実際に問題になるかは未計測。案 1 を採る場合は、
`ClipboardChanged` の発火頻度を実機で測ってから決めるのが妥当。

## 現状の記述

Windows Clipboard 側は、`InvokeInOrder` の XML コメントに
**隔離しているのは 2 者の間であって購読者どうしではない**ことを明記済み
（レビュー v2 の C-13 対応）。他プラットフォームの同種コメントは
「one bad subscriber cannot swallow the other's result」のままで、
実態より広い隔離を約束している。**案 2 を採る場合はここも直すこと。**
