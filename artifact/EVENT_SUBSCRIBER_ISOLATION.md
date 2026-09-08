# イベント購読者が互いから隔離されていない（横断課題）

- 記録日: 2026-09-07
- 分類: 横断課題（Windows Clipboard 固有ではない。Android / iOS / macOS / Windows の全 Manager に共通）
- 発見経緯: Windows Clipboard の実装レビュー v2（A-14）。遅延レンダリング担当のレビュアーが指摘し、
  Windows 固有の退行ではなく既存 Manager と共通の慣行であることを同時に確認した
- 対応方針: **Windows Clipboard で先に案 1 を適用し、他 3 プラットフォームは別タスクで追随する。**
  4 つ同時に変えると P2（他プラットフォームの既存ファイルを変更しない）に触れるため、
  実測できる 1 つで先に確かめてから広げる
- 進捗: **Windows 完了（2026-09-08）。Android / iOS / macOS は未着手**
- 更新: 当初は「Windows でも直さない」としていたが、レビュー v4 の
  「横断課題であることは Windows 側の契約違反が無い理由にはならない」を受けて方針を変更した

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

### Windows Clipboard のみ対応済み（2026-09-08）

`WindowsClipboardManager` は案 1 に移行した。共通イベントの発火は 6 か所あり、
引数あり／なしの 2 つのヘルパー（`RaiseIsolated`）に集約してある。

**この時点で挙動がプラットフォーム間で食い違っている。**
Windows では購読者 A の例外が B に波及しないが、他の 3 つでは波及する。
**この差は一時的なものであり、残り 3 つを揃えるまでが本課題である。**

計測についての訂正: 当初「`GetInvocationList` の割り当てが増える」を欠点に挙げたが、
これらのイベントはユーザーがコピーした時などにしか発火せず、毎フレームではない。
**割り当てを理由に案 1 を避ける根拠にはならない。**

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
  ただし上記のとおり、対象イベントの頻度では問題にならないと判断した（Windows で採用済み）

### 案 2: 現状を維持し、契約として明記する

「購読者は例外を投げてはならない」を XML ドキュメントに書き、隔離しない理由を残す。

- 利点: コスト 0、割り当てが増えない
- 欠点: 利用者側のバグが他の購読者の機能を静かに壊す。原因の特定が難しい

### 判断に必要なこと

**最終的には 4 プラットフォームで揃えること。** 揃うまでは
「macOS では届かないが Windows では届く」という差が残る。

Windows で案 1 を先に適用済みなので、**残り 3 つも案 1 に揃えるのが既定路線**である。
別の結論を採るなら、Windows 側を戻す判断も含めて改めて決めること。

## 現状の記述

Windows Clipboard 側は、`InvokeInOrder` の XML コメントに
**隔離しているのは 2 者の間であって購読者どうしではない**ことを明記済み
（レビュー v2 の C-13 対応）。Windows 側は案 1 適用後、
「他の Manager は 1 回の invocation を共有しており、その差は意図的で本ファイルに記録がある」
と書き換えてある。

他プラットフォームの同種コメントは
「one bad subscriber cannot swallow the other's result」のままで、
**実態より広い隔離を約束している。** 追随作業ではここも直すこと。
