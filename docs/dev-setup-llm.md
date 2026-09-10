# 開發環境：Word Analysis LLM（多語空耳）

API：`POST /api/v1/word/analyze`（需 JWT）。  
Prompt 定稿：[`docs/prompts/word-analysis.md`](prompts/word-analysis.md)（**多語優先**；泰語規則僅條件附錄）。

**權威來源（ADR-0013）：** 執行期 **ApiKey／Model／BaseUrl 只讀 SQLite**。App／Curator 都不持有金鑰。

## 1. 取得 API Key（建議 Google AI Studio）

1. 開啟 [Google AI Studio](https://aistudio.google.com/apikey) 建立 API Key。
2. 預設走 **OpenAI-compatible** 端點，現行 Flash：`gemini-3.6-flash`（官方 Gemini 3.6 Flash，GA；別名亦可見 `gemini-flash-latest`）。
3. 也可改用 OpenAI／其他相容端點（改 Model + BaseUrl）。

## 2. 寫入 SQLite（建議：策展後台）

允許清單帳號於策展站 `/llm-settings` 儲存 ApiKey／Model／BaseUrl，**立刻生效**。用量見 `/llm-usage`。

## 2b. 本機第一次 seed（可選）

若本機尚無策展、SQLite 仍空，可在 `src/Soraeru.Api` 用 User Secrets **僅供啟動時一次性 seed**：

```powershell
cd src\Soraeru.Api
dotnet user-secrets set "Llm:ApiKey" "YOUR_API_KEY_HERE"
dotnet user-secrets set "Llm:Model" "gemini-3.6-flash"
dotnet user-secrets set "Llm:BaseUrl" "https://generativelanguage.googleapis.com/v1beta/openai"
```

API 啟動後會把上述值寫入 SQLite；之後改設定請走策展，不必再依賴 User Secrets／`Llm__*`。

`appsettings*.json` 的 `Llm:ApiKey` 請保持空字串。

## 3. 啟動 API

```powershell
$env:NUGET_PACKAGES = "D:\.nuget\packages"
dotnet run --project src\Soraeru.Api
```

DB 尚未有 Key 時，分析會回 **503** `LLM_NOT_CONFIGURED`。

## 4. 手動驗證（curl）

先登入拿 JWT，再分析（示例：泰文「ขอบคุณ」、日文、英文皆可）：

```powershell
# Login
$login = Invoke-RestMethod -Method POST -Uri http://localhost:5080/api/v1/auth/login `
  -ContentType "application/json" `
  -Body '{"email":"test@example.com","password":"password123"}'
$token = $login.accessToken

# Analyze Thai
Invoke-RestMethod -Method POST -Uri http://localhost:5080/api/v1/word/analyze `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" `
  -Body (@{
    text = "ขอบคุณ"
    sourceLanguage = "auto"
    memoryLanguage = "zh-TW"
    notationPreference = "bopomofo"
  } | ConvertTo-Json)
```

亦可用 `src/Soraeru.Api/Soraeru.Api.http` 的 Analyze 區段。

## 5. App（L06 → L09 → L10）

1. 啟動 API（DB 已有 LLM 設定，或完成 seed）。
2. 以 Windows 或 Android 開 App、登入。
3. 首頁 → 手動輸入 → 輸入任意外語單字 → 開始分析。
4. Analyzing 會呼叫真實 API；結果頁顯示偵測語言、詞義、讀音、2～3 空耳候選（非硬編碼示範）。

重新產生會再打 API（命中程序內快取則 `cached: true` 且不扣額度）。
