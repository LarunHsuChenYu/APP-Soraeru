# 本機跑策展端（票 05）

獨立 Blazor Server 站，打共用 API。決策見 [ADR-0012](adr/0012-curator-blazor-server.md)、[ADR-0013](adr/0013-llm-runtime-settings-sqlite.md)。

## 兩種連線目標

| 目標 | `Curator:ApiBaseUrl` | 何時用 |
|---|---|---|
| **本機 API（建議先測）** | `http://localhost:5080` | 驗證策展 UI／新端點；不必部署 |
| **Railway 正式 API** | `https://airy-enjoyment-production-de0f.up.railway.app` | 管正式庫；**須先把含 0013 的 API 部署上去**，否則 `/curator/llm/*` 會 404 |

本機預設＝第一列。先不用部署時，請走本機 API。

## 啟動（本機一組）

兩個終端：

```powershell
# 1) API
dotnet run --project src/Soraeru.Api --launch-profile http
# → http://localhost:5080/health

# 2) 策展
dotnet run --project src/Soraeru.Curator --launch-profile http
# → http://localhost:5180
```

瀏覽器開：`http://localhost:5180/login`

## 設定

`Curator` 區段（`appsettings.Development.json` 或 User Secrets）：

| 鍵 | 說明 |
|---|---|
| `ApiBaseUrl` | 本機 `http://localhost:5080`；連 Railway 則改正式網址 |

允許清單＝API 的 `DeveloperAccounts`（目前含 `larun70@gmail.com`、`avai.hsu@gmail.com`、`aben8622@gmail.com`）。登入後與 DB `IsDeveloper` **取聯集**：名單或旗標任一為真即為開發者（無限額度）。策展 UI「帳號」頁可切換旗標／重置密碼，不必為每位使用者改 appsettings。

本機策展用 **Email／密碼**登入；帳密必須存在於**同一個本機 API** 的 SQLite（與 Railway 正式庫不是同一本）。App 上設過開發者／無限額度若只打過 Railway，本機仍要另外有帳。

連 Railway 時：JWT／帳號資料在雲端那份 DB；新策展／LLM／帳號端點須先部署才有。

## 煙測清單

1. 允許清單帳號登入 → 看到「已驗證空耳」  
2. 非清單帳號登入 → `/access-denied`（除非 DB 已標 `IsDeveloper`）  
3. 新增／編輯／啟用下架一條金標  
4. **LLM 設定** `/llm-settings`：改 Model／BaseUrl／ApiKey → 儲存  
5. **LLM 用量** `/llm-usage`：本機再打一次分析後應出現紀錄  
6. **帳號** `/accounts`：列表 → 切換開發者 → 重置密碼（≥8 字）  

若只測策展 CRUD／設定頁，可不開 App；測「分析吃到新 Key／用量列」需 JWT 呼叫 `POST /api/v1/word/analyze`（或 App Debug 指本機）。

## 正式使用（生產）

**拓樸（已定）：** 沿用既有 Railway **API** 服務（同一 SQLite Volume／帳號／額度／LLM）＋**另開一條** Curator Blazor Server 服務。不要為策展再複製一套 API／DB（除非刻意做 staging）。票 06 學習端 Web 仍 deferred，此處的「Web」＝策展站。

**誰用／怎麼登入：** 僅 `DeveloperAccounts` 或 DB `IsDeveloper` 的策展者。目前 UI 為 **Email／密碼**（打共用 API 登入）；Google 登入尚未接上正式頁。帳密必須存在於**目標 API 的那份 DB**（本機 SQLite ≠ Railway Volume）。

**LLM key：** 放在 **API**（Railway Variables 的 `Llm__ApiKey`，或策展「LLM 設定」寫入 SQLite 覆寫，見 ADR-0013）。Curator 容器**不**持有 LLM 金鑰。

**本機 vs 正式：** 本機策展預設打 `localhost:5080`；正式策展必須設 `Curator__ApiBaseUrl=https://airy-enjoyment-production-de0f.up.railway.app`（無尾斜線亦可）。改正式庫＝改真實使用者／金標／用量，部署前先確認目標。

**部署前程式準備：** 根目錄 `Dockerfile.curator` 只建置 Curator；服務支援 Railway `PORT` 並提供匿名 `/health`。Curator 不掛 Volume，也不持有 API／LLM secrets。分階段驗證與 Railway Dashboard 的 Root Directory、Dockerfile Path、Variables、healthcheck、domain 操作見 [dev-setup-railway.md](dev-setup-railway.md)「策展服務」一節；實際部署順序為既有 API 先、Curator 後。
