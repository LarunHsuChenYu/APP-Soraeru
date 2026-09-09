# 05 — 策展端 Blazor：允許清單登入＋最小 CRUD

**What to build:** 策展者用允許清單內 Google 帳號登入**獨立**策展站，以 UI 維護已驗證空耳（新增／編輯／啟用下架／列表搜尋）；非清單帳號進不了維護面。另：LLM 用量觀測與 ApiKey／Model／BaseUrl 執行期覆寫（ADR-0013）。

**Blocked by:** 04 — 已驗證空耳管理 API＋分析金標優先覆寫（done）

**Status:** in-progress

## Resume（2026-09-04）

App MVP 功能閉環（07–10）已完成；解除 App-first 延後。宿主鎖定 **Blazor Server**（ADR-0012）。過渡期 API 仍可用；本票補獨立策展 UI。擴充選 A：LLM 設定寫入 SQLite 立刻生效。

## Parent

[`docs/specs/parallel-web-curator-trust.md`](../specs/parallel-web-curator-trust.md) · ADR-0003、0005、0006、**0012**、**0013**

## What to build

新建並分開部署的策展端 Blazor Server 應用。流程：Google 登入（本機可輔以 Email）→ email ∈ 允許清單（`IsDeveloper`／`DeveloperAccounts`）才進入維護 UI；對 04 的管理 API 做最小 CRUD 畫面（語言、原詞、displayText、notationText、explanation、啟用／下架、列表／搜尋）。非允許清單不得使用策展 UI。不開社群投稿／審核佇列。驗證上架後，學習者側分析同鍵命中行為已由 04 保證；本票以策展者操作路徑可 demo 為驗收。

另：策展可檢視 `LlmUsage`、變更執行期 LLM ApiKey／Model／BaseUrl（SQLite 覆寫）；可管理帳號列表、開發者旗標、重置密碼。

## Acceptance criteria

- [ ] 允許清單內 Google 帳號可登入策展站並看到維護介面。（**手動煙測待做**；Email 路徑已接）
- [x] 非允許清單帳號無法進入維護面（即使 Google 登入成功）。（UI 閘門單測綠；API 仍 403；DB `IsDeveloper` 與名單聯集後可進）
- [ ] 策展者可於 UI 新增、編輯、啟用／下架已驗證空耳，並列表／搜尋。（**UI 已實作；手動煙測待做**）
- [x] UI 寫入走共用 API；不另起第二業務後端。
- [x] 獨立於 Web 學習端部署／專案邊界清楚（`Soraeru.Curator`；ADR-0012）。
- [ ] 煙測或手動腳本可示範：UI 上架一條 → 學習者分析同鍵拿到已驗證空耳（依賴 04 行為）。
- [x] 策展者可查看遮罩金鑰與今日用量摘要；可變更 ApiKey／Model／BaseUrl（SQLite，立刻生效）。
- [x] 文字分析寫入 `LlmUsage`（`text_analysis`）；非允許清單無法呼叫 LLM 管理 API。
- [x] 策展者可於「帳號」頁列出使用者、切換 `IsDeveloper`（連動額度）、重置密碼（≥8）；登入 `SyncDeveloperFlag`＝名單 ∪ DB。

## Notes（2026-09-04）

- 專案：`src/Soraeru.Curator`（Blazor Server，`http://localhost:5180`）
- 測試：Application.Tests 62 綠（含 CuratorUserAdmin／Auth developer sync）；Curator.Tests 6 綠
- 本機說明：[`docs/dev-setup-curator.md`](../dev-setup-curator.md)
- ADR-0013：[`docs/adr/0013-llm-runtime-settings-sqlite.md`](../adr/0013-llm-runtime-settings-sqlite.md)
- API：`GET/PUT /api/v1/curator/llm/settings`、`GET /api/v1/curator/llm/usage`
- API：`GET /api/v1/curator/users`、`PATCH .../developer`、`POST .../reset-password`
- 策展頁：`/llm-settings`、`/llm-usage`、`/accounts`
- 下一步：本機煙測後勾剩餘 AC／改 done
