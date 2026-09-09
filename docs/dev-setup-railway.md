# 生產環境：Railway 部署 Soraeru.Api

讓 API 以 Docker 跑在 Railway，SQLite 放 Volume。**密鑰只放 Railway Variables，不要寫進 git／`appsettings.json`。**

決策見 [ADR-0009](adr/0009-railway-sqlite-volume.md)。本機建置路徑見 [dev-setup-build.md](dev-setup-build.md)。

## 1. 映像怎麼建

- 方案**根目錄** `Dockerfile`：restore／publish **只** `src/Soraeru.Api`（會帶入 Application、Infrastructure）。
- 不要把 `Soraeru.slnx` 整包丟進 SDK 映像——方案含 MAUI，官方映像沒有 Android workload。
- Railpack 0.37+ 雖列出 Dotnet，但**只在倉庫根目錄找 `*.csproj`**。本 repo 的 API 在 `src/Soraeru.Api/`，根目錄又是 `.slnx`（含 MAUI），Railpack 會失敗（`could not determine how to build`／`start.sh not found`）。**必須用 Dockerfile**，不要讓服務走 Railpack。
- 可選根目錄 `railway.toml`（builder=DOCKERFILE、healthcheck `/health`）。若該服務是 Railway **新服務**且 Config as Code 已被忽略，請在 Dashboard 手動設：
  - **Builder = Dockerfile**（根目錄 `Dockerfile`；不要選 Railpack）
  - Healthcheck Path = `/health`
  - Replicas = **1**

## 2. Volume

| 項目 | 值 |
|------|-----|
| Mount path | `/app/data` |
| 連線字串 | `Data Source=/app/data/soraeru.db` |

掛上 Volume 後目錄是空的；API 會 `CreateDirectory`，再 `Migrate()` 建檔。掛完請 **Redeploy**。

**Replicas 必須為 1。** 多副本會互搶同一個 SQLite 檔。

## 3. Variables（密鑰只放這裡）

在 Railway 服務 Variables 設定（雙底線 = 巢狀設定）：

| 變數 | 說明 |
|------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:${{PORT}}`（與 `Program.cs` 讀 `PORT` 雙保險） |
| `ConnectionStrings__Default` | `Data Source=/app/data/soraeru.db` |
| `Jwt__SigningKey` | ≥32 字；**與本機 User Secrets 不同**的正式密鑰 |
| `GoogleAuth__ClientIds__0` | 現有 Web Client ID |
| `GoogleAuth__ClientIds__1` | 現有 Android Client ID（若有） |
| `Llm__ApiKey` | Gemini／相容端點金鑰 |
| `Llm__Model` | 可選覆寫 |
| `Llm__BaseUrl` | 可選覆寫 |
| `Cors__AllowedOrigins__0` | **先不要設**。Web 上線後再填 `https://<web>.up.railway.app` |

`Jwt:Issuer`／`Jwt:Audience` 沿用 `appsettings.json`（`Soraeru`／`Soraeru.App`）即可。

產生 JWT 密鑰（本機、不要貼進聊天紀錄或 commit）：

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 } | ForEach-Object { [byte]$_ }))
```

## 4. Dashboard 步驟（同一 GitHub repo、一個 API 服務）

> **介面提示（2025–2026）：** Railway **沒有**專案列表上的「Build」分頁。建置選項在 **服務（Service）→ Settings** 裡往下捲的 **Build** 區塊。若只看到 `0/1 service online` 的專案卡片，代表你還在專案層——請點進服務方塊。

1. 將含 Dockerfile 的 commit 推到 GitHub（`origin`：`APP-Soraeru`）。
2. [Railway](https://railway.com/) → 點專案（例如 `satisfied-celebration`）→ **點 Canvas 上的服務方塊**（不是 Project Settings 齒輪）。
3. 上方分頁應為 **Deployments｜Variables｜Metrics｜Settings**。
4. **Settings** → 往下捲 **Source**：repo = `APP-Soraeru`、branch = `main`、Root Directory 留空。
5. 同頁 **Build** 區塊：**Builder = Dockerfile**（不要 Railpack）；Dockerfile Path 留空或 `Dockerfile`。
6. **Variables** → 往下捲 **Volumes** → mount `/app/data` → Redeploy。
7. **Settings** → Scaling → **Replicas = 1**。
8. **Settings** → Networking → **Generate Domain**；Deploy 區 Healthcheck = `/health`。
9. 填上節 3 的 Variables → Redeploy。

驗證：`GET https://<api>.up.railway.app/health` 應為 200。再註冊一筆帳、Redeploy，確認 Volume 上的 `soraeru.db` 還在。

## 4.1 常見錯誤：Railpack `could not determine how to build`

Railway 預設用 **Railpack**。若 GitHub 還沒有根目錄 `Dockerfile`，或服務 Builder 仍是 Railpack，日誌會類似：

- `⚠ Script start.sh not found`
- `✖ Railpack could not determine how to build the app`
- 分析到的檔案只有 `Soraeru.slnx`、`src/`、`nuget.config`，**沒有** `Dockerfile`／根目錄 `.csproj`

處理：

1. 確認 `Dockerfile`、`.dockerignore` 已 **commit 並 push** 到 Railway 所接的 GitHub 分支（通常是 `main`）。
2. 進 **服務方塊 → Settings**，往下捲 **Build** → **Builder = Dockerfile**（Dockerfile path 留空或 `Dockerfile`）。
3. **Deployments** → 最新一筆 → **Redeploy**（部分情況 Git push 仍走 Railpack，手動 Redeploy 才會用 Dockerfile）。
4. 成功的建置日誌應出現 `Using detected Dockerfile!` 與 `FROM mcr.microsoft.com/dotnet/sdk:10.0`，而不是 `Railpack 0.x`。

若 **Settings 裡找不到 Builder 下拉** 或欄位鎖住：

- **Variables** 加 `RAILWAY_DOCKERFILE_PATH=Dockerfile`，再 Redeploy；或
- 刪除失敗服務 → Canvas **+ New** → **GitHub Repo** → 建立時選 **Dockerfile**；或
- 根目錄 `railway.toml` 已設 `builder = "DOCKERFILE"`，但**新服務**可能忽略 Config as Code——仍以 Dashboard 手動設為準。

## 5. 已知限制（上線知情）

- 忘記密碼：token 在行程記憶體，信只進 log；封閉測試請用 Email 註冊或 Google。
- 分析快取是行程內記憶體：redeploy 後清空（可能再扣額度）。
- SQLite = MVP 單實例；要多區／多副本再遷 Postgres（另開 ADR）。
- App **Android Release／封閉測試 APK** 預設打公開 HTTPS（`MauiProgram.ResolveApiBaseUrl` → `https://airy-enjoyment-production-de0f.up.railway.app/`）。**Debug** 模擬器仍用 `http://10.0.2.2:5080/`。切回本機 Release：編譯常數 `USE_LOCAL_API`；Debug 強制雲端：`USE_RAILWAY_API`。

## 6. CORS（現在空、Web 之後）

`Cors:AllowedOrigins` 預設空陣列 → **不加**跨域中介，Android App 不受影響。
**策展 Blazor Server** 以伺服器端 `HttpClient` 打 API → **不必**為 Curator 加 CORS。
票 06 **學習端 Web**（瀏覽器直打 API）上線後再設 `Cors__AllowedOrigins__0`。

## 7. 策展服務（Curator＝票 05「Web」）

與 ADR-0012／ADR-0009 一致：**同一個 repo、第二條 Railway 服務**；**共用既有 API**，不要新開第二套 API／Volume（除非要完整 staging）。Curator 必須以 monorepo **Root Directory 隔離**，使建置 context 完全看不到 repo 根目錄的 API `railway.toml` 與 `Dockerfile`。

### 第 1 階段：程式準備（本機完成，不部署）

- [x] `src/Soraeru.Curator/Dockerfile` 以該子目錄為完整 build context，只 restore／publish Curator。
- [x] `src/Soraeru.Curator/.dockerignore` 排除 `bin`／`obj`／DB／常見憑證檔。
- [x] Curator `.csproj` 沒有 `ProjectReference`，也不依賴 context 外的 `Directory.Build.props`／`Directory.Build.targets`。
- [x] Curator 支援 Railway `PORT`，並提供匿名 `GET /health`。
- [x] Production 不強制把 Railway 內部 HTTP 重新導向 HTTPS；公開 TLS 由 Railway termination。
- [ ] 發版前重新執行 Curator tests／build、API build，以及可用時的 Curator Docker build。

### 第 2 階段：Railway Dashboard（取得人工授權後執行）

部署順序固定為 **既有 API → 新 Curator**：

1. 先讓既有 **API 服務**使用含 `/api/v1/curator/*`（verified CRUD、LLM、accounts）的版本；確認 `GET https://airy-enjoyment-production-de0f.up.railway.app/health`＝200。保留既有 `/app/data` Volume 與單 replica。
2. Canvas **+ New** → **GitHub Repo** → 選同一 repo，建立第二個服務（建議名稱 `soraeru-curator`）。
3. Curator 服務 → **Settings → Source → Add Root Directory**，填入 `/src/Soraeru.Curator`。若 UI 自動移除前導 `/`，最後顯示 `src/Soraeru.Curator` 是同一設定。
4. 同頁 **Build** 確認 Builder 為 `Dockerfile`，並將 **Dockerfile Path 清空**；Railway 會使用隔離根目錄內的 `Dockerfile`。不要設定 Custom Config File，也不要設定 `RAILWAY_DOCKERFILE_PATH`。
5. **不要掛 Volume**；Curator 只透過 HTTP 使用既有 API。
6. Curator 服務 → **Variables**：

| 變數 | 值 |
|------|-----|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Curator__ApiBaseUrl` | `https://airy-enjoyment-production-de0f.up.railway.app` |

   `PORT` 由 Railway 注入，程式會直接讀取；不需要 `ASPNETCORE_URLS`。Curator **不需要也不應設定** `Jwt__SigningKey`、API 的 Connection String 或 LLM key；它透過 `Curator__ApiBaseUrl` 呼叫既有 API。
7. **Settings → Deploy** 將 Healthcheck Path 設為 `/health`。
8. **Settings → Networking → Generate Domain**，取得 `https://<curator>.up.railway.app`。
9. 取消目前錯誤部署，儲存上述設定後從 **staged changes** 按 **Deploy**；確認建置 context 是 `src/Soraeru.Curator`、啟動項為 `Soraeru.Curator.dll`，而不是 `Soraeru.Api.dll`。
10. 部署成功後先驗證 Curator `/health`＝200，再以允許清單 Email 登入，煙測金標 CRUD、LLM 設定／用量、帳號管理。

Repo 根目錄的 `Dockerfile.curator` 與 `railway.curator.toml` 是舊方案相容檔，不再用於 Curator Railway 服務。不要把 Curator Root Directory 改回 repo 根目錄，也不要用 custom config 或 `RAILWAY_DOCKERFILE_PATH` 覆寫隔離設定。

### 日常更新

| 變更範圍 | 動作 |
|----------|------|
| 只改 API／Infrastructure | push → **API 服務** Redeploy；EF `Migrate()` 啟動時跑；Volume 保留 |
| 只改 Curator UI | push → **Curator 服務** Redeploy |
| 兩者都改 | **先 API、再 Curator**，確保 Curator 依賴的端點已上線 |

回滾：Railway Deployments → 上一成功版 Redeploy。SQLite Volume **不要**刪；分析記憶體快取會因 Redeploy 清空（已知限制）。
