# 策展端採 Blazor Server（獨立站）

策展端（票 05）以 **Blazor Server** 實作並**獨立部署**；不塞進 API 容器。瀏覽器經 Google（或開發用 Email）取得共用 API 的 JWT 後呼叫策展管理端點；允許清單語意仍由 API／`DeveloperAccounts` 把關（ADR-0005）。

**Status**: accepted  
**Decided in**: 2026-09-04（票 05 開工；宿主選定）

## Considered Options

- **Blazor Server（選定）**：內部小後台上手快；獨立 ASP.NET 宿主即可，不必 WASM 打包鏈。  
- **Blazor WASM**：較像 App 的「瀏覽器打 API」，設定與除錯成本較高，MVP 不取。  
- **Unified／混用**：本票不需要。  
- **同容器塞進 API**：已於 ADR-0009 拒絕。

## Consequences

- 方案新增 `Soraeru.Curator`；Railway／本機各開一條服務。  
- Web 學習端（票 06）宿主仍可另選，本 ADR 不鎖定票 06。  
- UI 煙測為主；允許清單閘門可單元測。
