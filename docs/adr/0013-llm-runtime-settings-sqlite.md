# ADR-0013 — LLM 執行期設定存 SQLite＋LlmUsage 觀測

策展端可變更 **ApiKey／Model／BaseUrl／System Prompt（含詞義／讀音版）**，寫入 **SQLite**（Railway Volume），**不必 Redeploy** 即生效。  
**請求時 ApiKey／Model／BaseUrl 只讀 SQLite**，不再 fallback `appsettings`／Railway `Llm__ApiKey`／`Llm__Model`／`Llm__BaseUrl`。  
**System Prompt**：DB 有值則用 DB；清空則回落程式內嵌 `WordAnalysisPrompts`（與 `docs/prompts/word-analysis.md` 同步之預設）。  
首次啟動若 DB 缺欄位，可**一次性**從設定檔／環境變數 seed 金鑰與端點，並從內嵌 Prompt seed；之後即可自 Railway 移除 `Llm__*`。  
Provider **已鎖定 Gemini**（OpenAI-compatible：`gemini-3.6-flash` + `https://generativelanguage.googleapis.com/v1beta/openai`）；不再評估 OpenRouter／混合入口。  
每次文字分析 LLM 呼叫寫入 **LlmUsage**，供策展後台檢視。金鑰永不回傳明文（僅遮罩）；僅允許清單策展者可讀寫。

**Status**: accepted（2026-09-10 修訂：DB-only 請求解析；2026-09-22 修訂：System Prompt 可策展覆寫＋Gemini 鎖定）  
**Decided in**: 2026-09-04（票 05 擴充；選 A）；修訂 2026-09-10；修訂 2026-09-22  
**Related**: ADR-0011、ADR-0012；金鑰／Provider 決策以本 ADR＋策展 LLM 設定為準（舊計畫中的 OpenRouter／混合路線已不採用）

## Considered Options

- **SQLite 為唯一來源＋立刻生效（選定）**：策展 UI 運維；Volume 持久；請求不讀 Railway LLM 變數。  
- **SQLite 覆寫＋env fallback（舊）**：易與 Railway Variables 混淆；已於 2026-09-10 廢止。  
- **僅環境變數**：需 Redeploy；已拒。  
- **Key 進策展前端／App**：違反 CMO／既有安全邊界；已拒。  
- **Prompt 僅程式常數**：改 prompt 需發版；已擴充為策展可寫 SQLite（仍保留內嵌預設作清空回落）。  
- **OpenRouter／混合 Provider**：已拒——生產固定 Gemini 直連。

## Consequences

- `LlmRuntimeSettings` 單列；ApiKey／Model／BaseUrl **必須在 DB** 才可供分析。  
- `SystemPrompt`／`MeaningReadingOnlySystemPrompt` 可選；空則用內嵌預設。  
- 啟動時 `LlmRuntimeSettingsBootstrap`：DB 不完整時才從 config／內嵌 Prompt seed。  
- `LlmOptions` 仍可提供 `TimeoutSeconds`／本機開發 seed；**不得**作為請求時金鑰來源。  
- `OpenAiCompatibleWordAnalysisAgent` 每次請求 `ResolveAsync`（Authorization／BaseUrl／Model／System Prompt 皆經 resolver）。  
- App／Curator **不**持有 LLM 金鑰；只呼叫 API。  
- `LlmUsage` 先接 `text_analysis`；Vision 槽日後同表 `feature_type`。  
- ADR 編號：金鑰策略原計畫稱 0012，但 0012 已用於策展 Blazor Server，故本決策為 **0013**。
