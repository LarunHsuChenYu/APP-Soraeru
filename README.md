# Soraeru（空耳聯合國）

三層式 .NET 方案骨架。規格以 `docs/` 規劃書為準。

## 專案

| 專案 | 層級 | 職責 |
|---|---|---|
| `src/Soraeru.Api` | Presentation | Minimal API、Endpoints、HTTP 契約 |
| `src/Soraeru.Application` | Business | Use cases、Abstractions、驗證 |
| `src/Soraeru.Infrastructure` | Data | Repository／Token／Mail／LLM／EF 實作 |
| `src/Soraeru.App` | Client Presentation | MAUI Android UI；內部分 Pages／ViewModels／Services／Data |

## 依賴方向

```text
Soraeru.Api → Soraeru.Application
Soraeru.Api → Soraeru.Infrastructure
Soraeru.Infrastructure → Soraeru.Application
Soraeru.App ──HTTPS──► Soraeru.Api
```

Application 只定義介面；Infrastructure 實作並在 API 的 DI 註冊。App 不直接參考 Application／Infrastructure。

## 建置

Windows 本機 NuGet 仍強制短路徑（`Directory.Build.props` → `D:\.nuget\packages`）；Linux／Docker 走預設 `~/.nuget/packages`。若 VS 仍出現 Import／MAX_PATH 或路徑含 `cursor-sandbox-cache`（常見於 Agent restore 後殘留污染 `obj`），見 [docs/dev-setup-build.md](docs/dev-setup-build.md)。一鍵修復（請先關 VS，在一般 PowerShell）：

```powershell
.\scripts\fix-nuget-path.ps1 -Build
```

```bash
dotnet build Soraeru.slnx
dotnet run --project src/Soraeru.Api
```

API 預設聽在 `http://localhost:5080`。驗證請開 **`http://localhost:5080/health`**（應回 `{"status":"ok",...}`）；根路徑 `/` 會導向 `/health`。VS 選 `http` profile 時 `launchUrl` 已是 `health`。生產部署見 [docs/dev-setup-railway.md](docs/dev-setup-railway.md)。

## Auth（已打通 Email／JWT）

| Method | Path | 說明 |
|---|---|---|
| POST | `/api/v1/auth/register` | Email 註冊 |
| POST | `/api/v1/auth/login` | Email 登入 → JWT |
| POST | `/api/v1/auth/forgot-password` | 重設信（開發寫入 API log） |
| POST | `/api/v1/auth/reset-password` | 以 token 重設密碼 |
| GET | `/api/v1/me` | 需 Bearer；含 `isDeveloper`、剩餘額度 |
| DELETE | `/api/v1/me` | 需 Bearer；刪雲端鏡像單字本＋帳號（204） |

手動測：`src/Soraeru.Api/Soraeru.Api.http`。DB／開發者帳號步驟見 [`docs/dev-setup-db.md`](docs/dev-setup-db.md)。

## Word Analysis（多語空耳）

| Method | Path | 說明 |
|---|---|---|
| POST | `/api/v1/word/analyze` | 需 Bearer；單一 LLM → JSON（詞義／讀音／2～3 空耳） |

- Prompt 定稿（多語優先）：[`docs/prompts/word-analysis.md`](docs/prompts/word-analysis.md)
- LLM 設定（**SQLite 為準**；本機可選 user-secrets 僅供首次 seed）：[`docs/dev-setup-llm.md`](docs/dev-setup-llm.md)

```powershell
cd src\Soraeru.Api
# 可選：僅啟動時 seed 進 SQLite；之後請用策展 /llm-settings
dotnet user-secrets set "Llm:ApiKey" "YOUR_KEY"
```

App（Windows）預設打 `http://localhost:5080/`；Android 模擬器為 `http://10.0.2.2:5080/`。

### App 啟動與 Session

Splash 若發現本機有 JWT，會呼叫 `GET /api/v1/me`：成功則依 `onboardingCompleted` 進 **首次說明（L04）** 或 **首頁（L05）**，**不會再次顯示登入頁**。Token 無效（401／403）會清除 Session 後回登入頁。若要重看登入流程，請在設定頁按「登出」。
