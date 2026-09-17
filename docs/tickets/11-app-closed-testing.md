# 11 — App：封閉測試就緒與缺陷收斂

**What to build:** 功能閉環（07–10）完成後，產出可發給測試者的 Android 建置，跑過規劃書功能／資安檢核要點，並在封閉測試期收斂阻擋上架的缺陷。

**Blocked by:** 07、08、09、10（皆 done）

**Status:** ready-for-agent

## Parent

[`docs/AI 空耳外語學習 APP－MVP 系統規劃書/Cursor-MVP App 規劃書.md`](../AI%20空耳外語學習%20APP－MVP%20系統規劃書/Cursor-MVP%20App%20規劃書.md)（W6–W7、§12／§15）  
策略補充：[`docs/specs/parallel-web-curator-trust.md`](../specs/parallel-web-curator-trust.md) Further Notes（App-first）

## What to build

在 OCR、TTS、重產上限、隱私／設定收尾皆可 demo 後：準備封閉測試用建置與基本測試說明；對照規劃書驗收清單（登入、多語分析、OCR 選一字、TTS、單字本、額度、聲明）做回歸；建議接上 crash 監控。修測試中發現的阻擋級缺陷。目標軌道約 12 人×14 天（人數可依實際調整，但須有可追蹤的測試輪次）。不含正式商店素材定稿與送審（見 12）。

**單字本驗收（ADR-0007）：** 「單字本」以 Client-first 為準（票 13–17）——本機 SoT、未登入唯讀、已登入可離線寫、可選同步／金標不蓋已存個人空耳。若進封閉測試時 13–17 尚未全綠，須在檢核表標註缺口，**勿**把「僅 Server notebook 可存可查」當成 App 終局達標。

## Acceptance criteria

- [ ] 有可安裝的封閉測試建置與簡短測試說明（含帳號／環境注意）。
- [ ] 規劃書 §15.1 功能要點可逐項勾選或記錄缺口與修復；單字本條款對齊 ADR-0007／13–17（或缺口如實記錄）。
- [ ] §15.2 資安要點抽查：無 LLM Key 於 App、HTTPS＋JWT、不傳原圖。
- [ ] 測試期阻擋級缺陷有收斂紀錄（修畢或明確延後理由）。

## Blocked by

- 07 — App：裝置端 OCR 選一字進分析
- 08 — App：播放正式發音（系統 TTS）
- 09 — App／API：同字重產 ≤3 與分析錯誤態
- 10 — App：隱私／AI 聲明與設定收尾

## Notes（缺陷收斂・2026-08-13／08-21）

- 封閉測試整包尚未開工（仍 `ready-for-agent`）；前置手動驗證與阻擋級 UI 缺陷收斂進行中。
- **已驗證（08-13）**：02／08／10／16；09 行為＋「已達分析上限」文案。
- **已修（08-13）**：L00／L09 動畫黑塊（淺色 wash）；首頁 Tab 強制回 L05（Shell 絕對路由）；韓文硬閘文案；注音橫排；語言>5 下拉。
- **已修（08-21）**：單字本列表語言>5 picker `AutomationId` 重複設值 →「讀取失敗」（見票 13）；OCR session 清空＋來源語自動預選（見票 07）。
- **仍待**：封閉測試建置／§15 檢核表整包；07 實機多腳本 OCR 品質煙測；其餘 13 殘餘（離線 JWT、UsageDaily 孤兒）不阻擋本票開工。
- **API／打包（08-24）**：
  - Railway API 已上線（Dockerfile／Volume／公開網域／`/health`）；Android Release 預設 BaseAddress＝`https://soraeru.ash-ben.com/`（`MauiProgram.ProductionApiBaseUrl`）；Debug 模擬器仍 `10.0.2.2:5080`。
  - 曾試自訂網域 `tocc.top` 打包 APK → **SSL 主機名／憑證不符**（基礎設施憑證問題）；**不做** App 端 SSL bypass；已改回 Railway URL。若最後簽章 APK 仍指 tocc.top，需再打一包指向 Railway。
  - 切換見 `MauiProgram.ResolveApiBaseUrl`／`docs/dev-setup-railway.md`。狀態仍 `ready-for-agent`（§15 整包未勾）。
- **實機包／skill（08-26）**：
  - 多次打 **Release Signed APK** 供側載（路徑 `src/Soraeru.App/bin/Release/net10.0-android/com.soraeru.app-Signed.apk`；**不進 git**）。曾誤打 Debug，已改一律 Release。
  - 專案 skill：`.cursor/skills/soraeru-release-apk/` — 改 App 程式後自動打 Release APK。
  - 設定頁可看 **v1.0.1** 與成型時間，方便測試者對版。§15 整包仍未勾；狀態仍 `ready-for-agent`。
- **建置／路徑釐清（08-28）**：
  - VS 設計期雜訊（XAML type not found、Tesseract MSB3246、XA4301）與真實 **obj 快取損壞** 已區分；清 obj 後 Android **Release APK** 建置成功。
  - Signed APK 路徑：`src/Soraeru.App/bin/Release/net10.0-android/com.soraeru.app-Signed.apk`（**非** `Soraeru.Api` bin）。
