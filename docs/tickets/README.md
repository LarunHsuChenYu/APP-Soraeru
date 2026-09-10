# Tickets — App MVP 後恢復 Web／策展；上架軌並行

Parent（App MVP）: [`docs/AI 空耳外語學習 APP－MVP 系統規劃書/Cursor-MVP App 規劃書.md`](../AI%20空耳外語學習%20APP－MVP%20系統規劃書/Cursor-MVP%20App%20規劃書.md)  
Parent（信任閘／Web／策展）: [`docs/specs/parallel-web-curator-trust.md`](../specs/parallel-web-curator-trust.md)  
Parent（Client-first 單字本）: [`docs/specs/client-first-wordcards-sync.md`](../specs/client-first-wordcards-sync.md) · ADR-0007  
Parent（同字再查／本機短路）: [`docs/specs/local-notebook-lookup-short-circuit.md`](../specs/local-notebook-lookup-short-circuit.md) · ADR-0008  
Glossary: [`docs/glossary.md`](../glossary.md) · ADRs: [`docs/adr/`](../adr/)（0001–0012）

## 總覽（2026-09-10）

| 指標 | 數量 |
|---|---|
| **done** | 15 |
| **in-progress（WIP）** | 1（05） |
| **ready-for-agent** | 1（11） |
| **blocked** | 1（12） |
| **deferred** | 1（06） |
| **完成率** | 15／19＝79%（不含 deferred：15／18＝83%） |

**Frontier：**

- **[05](05-curator-blazor-crud.md)** — 策展端 Blazor Server：Railway 已上線；LLM／帳號／登入統計；金標 CRUD 煙測待勾
- **[11](11-app-closed-testing.md)** — 封閉測試就緒與缺陷收斂（上架軌；可與 05 並行）

## 手動驗證快照（2026-08-21）

| 票 | 手動狀態 | 備註 |
|---|---|---|
| **02** | ✅ 已驗證＋文案收斂 | 首頁草稿提示 OK；韓文夾 Hangul 改清楚「請重新產生」；允許注音腳本 |
| **08** | ✅ 已驗證 | TTS |
| **09** | ✅ 行為已驗證；UI 文案已修 | 達上限不再分析；詳情／結果「已達分析上限」；`RegenerateActionPresentation` 鎖文案 |
| **11／列表** | ✅ AutomationId 已修 | 語言>5 picker 不再重複設 AutomationId；「讀取失敗」已消 |
| **19** | ✅ 品牌圖示 | Deep Teal logo；MAUI icon／splash；L00／L01／L04＋Stitch 同步 |
| **13** | ✅ 本機 SQLite SoT | 多使用者 `OwnerUserId` 並存；JSON 一次性遷移；登出／401／換帳不清他帳 |
| **10** | ✅ 已驗證 | 隱私權政策、AI 內容聲明頁有內容 |
| **16** | ✅ 已驗證 | 可自行修改諧音（個人空耳） |
| **04** | 金標待策展建立 | 學習者不能自建；目前庫空則無「聽感已核定」屬預期（見票 Notes） |
| **07** | ✅ 混合 OCR＋體驗收斂 | 語系別文案；阿語翻拍預處理；西里爾短詞通用校正（ect/ест）；三按鈕例仍可能失敗 |
| L00／L09 | 黑塊已修 | `FloatingMnemonicBackground`／Analyzing 深色 wash 改淺色 Ellipse |
| 首頁 Tab | 已修 | Shell 絕對路由強制回 L05（`//main/HomePage`） |

## 策略（2026-09 更新）

App 功能閉環（07–10）已完成；**恢復策展 Blazor（05）**（宿主＝Blazor Server，ADR-0012）。Web 學習端（06）仍 deferred，待 05 後再開。  
04 的已驗證空耳 API 為 done；05 補獨立 UI。上架軌（11–12）可與 Web／策展並行。  
單字本長期形狀以 **ADR-0007 Client-first＋可選雲端同步** 為準（票 13–18；同字再查見 ADR-0008／票 18）。  
明確不在本輪：iOS、Billing UI、完整 SRS、雲端 OCR、多 Agent、即時協同／CRDT。

## 工單進度主表

| # | 分組 | 標題 | 狀態 | 目前狀況 | code-review-dual | 檢測方式 | Blocker／前置 | 下一步 | 殘餘風險 | 近期工時 |
|---|---|---|---|---|---|---|---|---|---|---|
| [01](01-application-tests-prefactor.md) | 信任閘／基礎 | Application 測試骨架（prefactor） | done | 測試宿主與 fake seam 就緒，紅綠循環可跑 | 已跑-有開放項 | 自動（`dotnet test`） | — | — | Standards 指出 EF Core 硬違規仍待後續收斂 | 0.25h（08-10） |
| [02](02-hard-gate-llm-draft.md) | 信任閘／基礎 | 後處理硬閘＋LLM 草稿標示 | done | `MnemonicHardGate`＋App 草稿橫幅已交付 | 已跑-有開放項 | 自動（Application.Tests）＋手動（草稿橫幅 UI） | 01 | — | **手動已驗證（08-13）**；Hangul 拒收文案已收斂；Standards 指出 EF Core 硬違規仍待後續收斂 | 0.5h（08-10）＋0.5h（08-13） |
| [03](03-notebook-end-to-end.md) | 信任閘／基礎 | 單字本端到端可存可查（歷史雲端鏡像） | done | 雲端鏡像 CRUD 基礎；角色已由 13–15 重釋 | 未跑 | 自動+手動 | — | — | 語意已過渡至 Client-first | 0.75h（08-10） |
| [04](04-verified-override-and-api.md) | 信任閘／基礎 | 已驗證空耳管理 API＋分析金標優先覆寫 | done | 策展 CRUD＋分析命中金標跳過 LLM | 已跑-有開放項 | 自動（Application.Tests）＋手動（verified 標示 UI） | 02 | 策展以 API 建金標後再驗標示 | **金標待策展建立**（非使用者自建；05 Blazor deferred） | 0.5h（08-11） |
| [05](05-curator-blazor-crud.md) | Web／策展 | 策展端 Blazor Server：金標 CRUD＋LLM 設定／用量 | **in-progress** | Railway 已部署；LLM／帳號／登入統計；側欄藏金標入口；金標端到端煙測待做 | 未跑 | 自動＋手動 | 04 done | Google 登入＋金標 CRUD demo 後勾 AC | 生產 Volume／靜態資源曾踩坑已修 | 3.0h（09-04）＋5.75h（09-09）＋1.75h（09-10） |
| [06](06-web-learner-mvp1.md) | Web／策展 | Web 學習端薄 MVP1 | **deferred** | 等 05；單字本＝雲端鏡像過渡 | 不適用 | 手動（Web 煙測） | 02、03；建議 05 後 | 05 後恢復 | — | — |
| [07](07-app-ocr-select-one.md) | App MVP | 裝置端 OCR 選一字進分析 | done | 混合 OCR；語系別文案；阿語翻拍預處理；西里爾短詞僅 ect/ест 校正 | 已跑-有開放項 | 自動+手動（實機 OCR 品質） | — | 西里爾三按鈕短詞仍可能失敗；多腳本實機品質 | **語言包約 37 MB**；螢幕翻拍／稀疏短詞不保證 | 0.5h（08-11）＋0.5h 規劃（08-13）＋1.5h（08-21）＋1.0h（08-24）＋3.75h（08-25）＋3.0h（08-26） |
| [08](08-app-tts-formal-reading.md) | App MVP | 播放正式發音（系統 TTS） | done | 結果／詳情／列表系統 TTS 已接 | 已跑-有開放項 | 自動+手動（TTS 實機聽感／缺語音包提示） | — | — | **手動已驗證（08-13）**；缺語音包提示僅文字，尚無深連結 | 1.5h（08-12）＋0.25h（08-13） |
| [09](09-app-regenerate-cap-and-errors.md) | App MVP | 同字重產 ≤3 與分析錯誤態 | done | `REGENERATION_LIMIT_EXCEEDED`＋App 錯誤態 | 已跑-有開放項 | 自動（App+API TDD）＋手動（達上限煙測） | — | — | **行為+UI 文案已修（08-13）**；缺 quota exceeded 行為測試 | 1.5h（08-12）＋1h（08-13） |
| [10](10-app-privacy-settings-polish.md) | App MVP | 隱私／AI 聲明與設定收尾 | done | LegalDocument＋設定入口；登入深色模式白字防禦；關於 v1.0.1／成型時間 | 已跑-有開放項 | 自動+手動（設定入口／onboarding 入口確認） | — | 系統深色模式複測登入字色 | **手動已驗證（08-13）**；商店託管 URL 留票 12 | 1.25h（08-12）＋0.25h（08-13）＋0.25h（08-25）＋0.25h（08-26） |
| [11](11-app-closed-testing.md) | App 上架 | 封閉測試就緒與缺陷收斂 | **ready-for-agent** | 07–10 全 done；Railway API 已上線；08-28 obj 快取排查後 Release APK 成功；§15 整包尚未勾 | 已跑-有開放項 | 手動（§15 檢核表；封閉測試整包） | 07–10（皆 done） | 確認 Railway Signed APK＋回歸清單＋§15 勾選 | Spec 指出 AC 未勾；tocc.top SSL 憑證待基礎設施修好 | 0.5h（08-13）＋0.25h（08-21）＋1.0h（08-24）＋1.0h（08-26）＋0.75h（08-28） |
| [12](12-app-play-store-submit.md) | App 上架 | 商店素材與送審 | **blocked** | 等 11 封閉測試通過 | 不適用 | 手動（Play Console） | 11 | 11 完成後準備素材 | — | — |
| [13](13-app-local-wordcard-store.md) | Client-first | 本機單字卡儲存與列表／存／刪 | done | **SQLite** SoT；明確登出保留本機；換帳／刪帳／401 不清他帳；列表 RefreshGate | 未跑 | 自動+手動（煙測） | — | 離線煙測見 Notes | 離線 JWT 過期偵測；UsageDaily 孤兒列 | ~4.25h（08-11）＋1.5h（08-13）＋0.75h（08-21）＋1.25h（08-24） |
| [14](14-app-sync-protocol-lww.md) | Client-first | 可選同步協定（LWW／tombstone／換帳） | done | Merger＋Coordinator＋前景觸發；假鏡像單測綠 | 已跑-有開放項 | 自動（ClientLogic.Tests） | 13 | 補端到端多裝置驗收 | EF Core 持續擴張；端到端多裝置待 15 現場驗 | 1.5h（08-12） |
| [15](15-server-notebook-mirror-api.md) | Client-first | Server Notebook API 雲端鏡像語意 | done | `GET/PUT mirror`＋`HttpCloudWordCardMirror` 已接 | 未跑 | 自動+手動（雙端 demo 待驗） | 14 | 現場雙端推拉 demo | 雙端 demo AC 未勾；Id 衝突 CONFLICT | 2.5h（08-12） |
| [16](16-app-edit-personal-mnemonic.md) | Client-first | 詳情頁隨時編修個人空耳 | done | `UpdateSelectedMnemonicAsync`＋詳情 UI | 已跑-有開放項 | 自動+手動 | 13 | — | **手動已驗證（08-13）** | 1h（08-12）＋0.25h（08-13） |
| [17](17-verified-mnemonic-no-overwrite-saved-card.md) | Client-first | 金標不覆蓋已存卡個人空耳 | done | Save 同鍵回傳既有卡；TDD 回歸鎖住 | 已跑-有開放項 | 自動（Application+ClientLogic） | 13；04 | 補整體驗收煙測 | dual review 已跑但仍有待收斂開放項 | 0.75h（08-12） |
| [18](18-app-local-notebook-lookup-short-circuit.md) | Client-first | 同字再查本機短路＋詳情重新分析 | done | ClientLogic Gate＋App 短路／重新分析已交付；code-review-dual 通過 | 已跑-有開放項 | 自動+手動（本機短路／重新分析煙測） | 13（done） | 封閉輪次勾選煙測 | 他機未同步卡仍會分析（ADR-0008 接受）；列表 AutomationId 已修見 13 | 1.5h（08-12） |
| [19](19-app-branding-icon.md) | App MVP | 品牌圖示與 L00／L01／L04 對齊 | done | Deep Teal logo；MAUI icon／splash；Stitch L00／L01／L04 同步 | 未跑 | 手動（launcher／splash／登入頁）＋Release APK | — | — | 商店截圖定稿仍留票 12 | 1.75h（08-28） |

> **code-review-dual 語意**：`未跑`＝尚無 Station 5 雙軸審查紀錄；`已跑-有開放項`＝已完成雙軸審查但仍有缺口或待確認項；`不適用`＝未開工或延後；勿將 `未跑` 視為通過。

## 依賴圖

```text
01 (prefactor tests) ──► 02 (硬閘 + LLM 草稿標示) ──► 04 (已驗證 API + 覆寫) ──┐
                                                                              │
03 (單字本端到端) ✓ done＝雲端鏡像歷史基礎 ──────────────────────────────────┤
                                                                              │
                         ┌── 07 (OCR 選一字)     ✓ done                     │
                         ├── 08 (TTS 正式發音)   ✓ done                     │
                         ├── 09 (同字重產≤3＋錯誤態) ✓ done                 │
                         ├── 10 (隱私／設定收尾) ✓ done                     │
                         │                                                  │
                         └──────────► 11 (封閉測試) ◄─ frontier ──► 12 (商店送審) │
                                                                              │
05 (策展 Blazor Server) ── in-progress（ADR-0012） ◄──────────────────────────┘
06 (Web 薄 MVP1) ── deferred（建議 05 後）；鏡像過渡（ADR-0007） ◄── 02、03

Client-first 單字本（App 主切片；與 08–10 平行安全）:

13 (本機 SoT 列表／存／刪) ✓ done
 │
 ├──► 14 (同步協定 LWW／tombstone／換帳) ✓ done ──► 15 (Server Notebook＝鏡像推拉) ✓ done
 │
 ├──► 16 (詳情頁編修個人空耳) ✓ done
 ├──► 17 (金標不覆蓋已存卡個人空耳) ✓ done
 └──► 18 (本機短路／詳情重新分析) ✓ done
```

## 與規劃書對照（缺口 → 票）

| 規劃 | 現況粗判 | 票 |
|---|---|---|
| W1 Auth | 大致完成 | 歷史 |
| W2–W3 分析＋單字本＋信任標示 | 01–04 done；單字本改 Client-first | **13–18** |
| W4 OCR | 07 done（實機品質待驗） | 歷史 |
| W4 TTS | 系統 TTS 已接（結果／詳情／列表） | **08** done |
| 同字重產 ≤3／錯誤態 | 後端上限＋App 錯誤態 | **09** done |
| 同字再查／本機短路 | App 本機查鍵短路＋詳情重新分析已交付 | **18** done |
| W5 設定／隱私／聲明 | 應用內隱私＋AI 聲明入口 | **10** done |
| W6–W8 封閉測試／上架 | 未開始；單字本驗收對齊 ADR-0007 | **11–12** |
| Web／策展 UI | 05 策展進行中；06 仍延後；Web 單字本＝鏡像過渡 | **05 in-progress；06 deferred** |

## Handoff → Station 4

每次新 session：**一張** frontier 票 + 對應 parent（parallel-web spec／ADR-0012 等）+ Testing Decisions＋先紅測再實作。

- **05 策展（現行 WIP）**：Blazor Server 獨立站；Google→API JWT；允許清單＝`IsDeveloper`；CRUD 打 `/api/v1/curator/verified-mnemonics`；不另起後端。  
- **06**：仍 deferred。  
- **封閉測試（11）**：上架軌可並行；§15 整包尚待。  
- **04 金標**：05 UI 上架後再驗 App verified 標示。  
- Client-first（13–18）done；封閉測試驗單字本以 ADR-0007／0008 為準。
