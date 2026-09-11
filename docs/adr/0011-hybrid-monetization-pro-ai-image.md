# ADR 0011 — Hybrid monetization: subscription-only, Pro AI image, dual billing

- Status: accepted
- Date: 2026-08-31
- Amended: 2026-08-31 (v2.1 CMO compliance)

## Context

CMO v1.3 定義三階梯漏斗（Free 5／獎勵廣告 +6／Pro NT$129）。工程需疊加 **AI 圖片候選**、Play Billing MVP，並與現行 `AppConstants.FreeDailyQuota = 20` 共存至封測結束。

曾考慮：Sora-Points 點數包、Free 看廣告解鎖 AI 圖、App 內導外 TapPay、自然月重置 AI 圖額度、UTC 日切、Vision 一段式直接產空耳。

## Decision

1. **純訂閱制**：僅 `Free`｜`Pro`；**不做**消耗品點數包。
2. **AI 圖 Pro 專屬**：固定 **30 次/訂閱週期**（**Billing Anchor** 重置，非自然月）；Free 與廣告加額皆不可用；管線為 **兩段式**；**0 候選不扣額**。
3. **混合日額**：`DailyFreeQuota` + `AdBonusQuota`；上線定案基礎 **5/日**；封測可暫留 **20**；日切 **Asia/Taipei 00:00**。
4. **雙金流、單一權益**：MVP **Google Play Billing**（App 唯一訂閱入口）+ Phase 2 **Web TapPay**（僅瀏覽器官網）；皆寫入 `PlanTier`／`SubscriptionExpiry`／`SubscriptionBillingAnchorAt`；App 只認 `GET /me`。
5. **Play Anti-Steering**：App **禁止**導外 `pay.soraeru.com`、比價或「官網更便宜」文案。
6. **廣告**：Free 僅 **Rewarded Video**（SSV 驗簽）；看完後 App **輪詢 `/me`** 再加額；Pro **零廣告**。
7. **單字卡降級**：超限 **唯讀鎖定**（可查不可新增）。
8. **本機 OCR 保留**：原圖不上雲；AI 圖路徑上雲前須 UI 明示。
9. **錯誤碼**：標準化 `QUOTA_*`、`AI_IMAGE_*`、`NOTEBOOK_LIMIT_EXCEEDED` + `guidance` payload。

權威規格：`docs/specs/hybrid-monetization-integrated.md` v2.1。

## Consequences

- 需實作 `SubscriptionGuard`、`AdMob SSV`、SSV 後輪詢、`LlmUsage`、Play RTDN；TapPay 僅 Web Phase 2。
- Pro 成本靠 **300/日軟上限** 與 **AI 圖 30/週期** 控管；上線後以 P95 token 校準。
- CMO v1.3 歸檔為歷史參考；署名 CTO 統一 **ASHTON**。
