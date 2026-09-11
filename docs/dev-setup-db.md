# 開發環境：SQLite 與 Email Auth

本機開工不需安裝 Azure SQL／SQL Server。預設使用 **EF Core + SQLite 檔案資料庫**。

## 1. 需要安裝什麼

| 項目 | 說明 |
|---|---|
| .NET SDK 10 | 建置／執行 API 與建立 migration |
| `dotnet-ef`（可選） | 手動跑 migration 時用；API **啟動時會自動 `Migrate()`** |
| DB 管理工具 | **非必要**。可用 [DB Browser for SQLite](https://sqlitebrowser.org/) 查看檔案 |

安裝 EF 工具（本機一次即可）：

```bash
dotnet tool install --global dotnet-ef
```

## 2. 連線字串在哪改

開發覆寫檔：`src/Soraeru.Api/appsettings.Development.json`

```json
"Persistence": {
  "Provider": "Sqlite"
},
"ConnectionStrings": {
  "Default": "Data Source=soraeru.dev.db"
}
```

- 路徑是**相對 API 程序工作目錄**（通常是 `src/Soraeru.Api`）。檔案會出現在該目錄下的 `soraeru.dev.db`。
- 改成 InMemory（無檔案、重啟清空）：`"Provider": "InMemory"`。
- 未來 SQL Server：將 `Provider` 換成對應實作並改連線字串（目前正式預設為 Sqlite）。

JWT 開發用密鑰同檔 `Jwt:SigningKey`（僅限 Development）。正式環境請用 **User Secrets／環境變數**，勿提交真實金鑰。生產 SQLite（Railway Volume）見 [`dev-setup-railway.md`](dev-setup-railway.md)。

## 3. 如何套用 Migration

方案已含初始 migration。兩種方式擇一：

**A. 啟動 API（推薦）** — 自動套用：

```bash
dotnet run --project src/Soraeru.Api
```

**B. 手動：**

```bash
dotnet ef database update --project src/Soraeru.Infrastructure --startup-project src/Soraeru.Api
```

新增 schema 變更後產生 migration：

```bash
dotnet ef migrations add <Name> --project src/Soraeru.Infrastructure --startup-project src/Soraeru.Api --output-dir Persistence/Migrations
```

## 4. 如何驗證 DB 已建立

1. 啟動過 API 後，確認檔案存在：`src/Soraeru.Api/soraeru.dev.db`（依連線字串）。
2. 用 DB Browser 開啟，應能看到 `Users`、`UsageDaily`、`__EFMigrationsHistory`。
3. 或註冊一位使用者後再開啟 `Users` 表查看列。

## 5. 開發者帳號（無限制）

允許名單在設定 `DeveloperAccounts`（Email **忽略大小寫**）：

- `larun70@gmail.com`
- `avai.hsu@gmail.com`
- `aben8622@gmail.com`

行為：

- **註冊**時若 Email 在名單內 → `IsDeveloper = true`，`DailyQuota` 設為極大值。
- **登入**時會再同步旗標（即使帳號先前已存在也會刷新）。
- `IQuotaService` 對 developer **不扣減**額度；`GET /api/v1/me` 回傳 `isDeveloper: true` 與極大的 `remainingDailyQuota`。

一般帳號（如 `test@example.com`）為 Free：`dailyQuota`／剩餘額度預設 **20**。

## 6. 快速驗收（curl）

```bash
# 註冊一般帳號
curl -s -X POST http://localhost:5080/api/v1/auth/register ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"test@example.com\",\"password\":\"password123\",\"displayName\":\"Tester\"}"

# 登入
curl -s -X POST http://localhost:5080/api/v1/auth/login ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"test@example.com\",\"password\":\"password123\"}"

# 把回傳的 accessToken 帶入
curl -s http://localhost:5080/api/v1/me -H "Authorization: Bearer <token>"
```

也可用 Visual Studio／Rider 開啟 `src/Soraeru.Api/Soraeru.Api.http`。

Google 登入設定見 [`docs/dev-setup-google-auth.md`](./dev-setup-google-auth.md)。


## 7. VS 建置失敗：DLL 被鎖定

若 Visual Studio 顯示無法複製 `Soraeru.Infrastructure.dll`／`Soraeru.Application.dll` 到 `bin\Debug`（檔案被另一程序鎖定），通常是先前 Api 仍在執行。

處理方式：

1. 在 VS 用紅色 **停止偵錯** 結束目前執行中的 Api（或關閉先前的 `dotnet run` 終端）。
2. 確認不要同時開兩個 Api 實例寫入同一輸出路徑（例如 VS F5 與另一個 `dotnet run` 並行）。
3. 仍鎖定時，於 PowerShell 結束本專案程序後再建置：`Get-Process Soraeru.Api -ErrorAction SilentlyContinue | Stop-Process -Force`
4. 重新在 VS 對 `Soraeru.Api` 按 F5／建置。

預設開發埠為 **5080**；若埠仍被佔用，多半還有殘留的 `Soraeru.Api` 行程。