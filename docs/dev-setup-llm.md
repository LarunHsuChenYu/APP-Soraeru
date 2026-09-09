# 開發環境：Word Analysis LLM（多語空耳）

API：`POST /api/v1/word/analyze`（需 JWT）。  
Prompt 定稿：[`docs/prompts/word-analysis.md`](prompts/word-analysis.md)（**多語優先**；泰語規則僅條件附錄）。

## 1. 取得 API Key（建議 Google AI Studio）

1. 開啟 [Google AI Studio](https://aistudio.google.com/apikey) 建立 API Key。
2. 預設走 **OpenAI-compatible** 端點，現行 Flash：`gemini-3.6-flash`（官方 Gemini 3.6 Flash，GA；別名亦可見 `gemini-flash-latest`）。
3. 也可改用 OpenAI／其他相容端點（改 `Llm:BaseUrl` + `Llm:Model`）。

## 2. 用 User Secrets 設定（勿提交 Key）

在 `src/Soraeru.Api` 目錄：

```powershell
cd src\Soraeru.Api
dotnet user-secrets set "Llm:ApiKey" "YOUR_API_KEY_HERE"
```

可選覆寫：

```powershell
dotnet user-secrets set "Llm:Model" "gemini-3.6-flash"
dotnet user-secrets set "Llm:BaseUrl" "https://generativelanguage.googleapis.com/v1beta/openai"
```

環境變數亦可：`Llm__ApiKey`。

`appsettings*.json` 的 `Llm:ApiKey` 請保持空字串；真實金鑰只放 User Secrets／環境變數。

**執行期覆寫（策展後台）：** 允許清單帳號可在策展站 `/llm-settings` 寫入 SQLite 覆寫 ApiKey／Model／BaseUrl，**立刻生效**（ADR-0013）。用量見 `/llm-usage`。

## 3. 啟動 API

```powershell
$env:NUGET_PACKAGES = "D:\.nuget\packages"
dotnet run --project src\Soraeru.Api
```

未設定 Key 時，分析會回 **503** `LLM_NOT_CONFIGURED`。

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

1. 啟動 API（含 LLM Key）。
2. 以 Windows 或 Android 開 App、登入。
3. 首頁 → 手動輸入 → 輸入任意外語單字 → 開始分析。
4. Analyzing 會呼叫真實 API；結果頁顯示偵測語言、詞義、讀音、2～3 空耳候選（非硬編碼示範）。

重新產生會再打 API（命中程序內快取則 `cached: true` 且不扣額度）。
