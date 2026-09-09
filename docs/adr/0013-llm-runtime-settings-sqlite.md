# ADR-0013 — LLM 執行期設定存 SQLite＋LlmUsage 觀測

策展端可變更 **ApiKey／Model／BaseUrl**，寫入 **SQLite**（Railway Volume），**不必 Redeploy** 即生效；設定覆寫優先於 `appsettings`／環境變數。每次文字分析 LLM 呼叫寫入 **LlmUsage**，供策展後台檢視。金鑰永不回傳明文（僅遮罩）；僅允許清單策展者可讀寫。

**Status**: accepted  
**Decided in**: 2026-09-04（票 05 擴充；選 A）  
**Related**: [AI API Key 策略計畫](c:\Users\ashtonhsu\.cursor\plans\ai_api_key_策略_a71b129f.plan.md)、ADR-0011、ADR-0012

## Considered Options

- **SQLite 覆寫＋立刻生效（選定）**：策展 UI 可運維；Volume 持久。  
- **僅環境變數**：需 Redeploy；已拒為本需求。  
- **Key 進策展前端／App**：違反 CMO／既有安全邊界；已拒。

## Consequences

- `LlmRuntimeSettings` 單列覆寫；欄位 null＝沿用設定檔。  
- `OpenAiCompatibleWordAnalysisAgent` 每次請求解析有效設定（含 Authorization／BaseUrl）。  
- `LlmUsage` 先接 `text_analysis`；Vision 槽日後同表 `feature_type`。  
- ADR 編號：金鑰策略原計畫稱 0012，但 0012 已用於策展 Blazor Server，故本決策為 **0013**。
