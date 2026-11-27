# ADR-0014 — 生產文件全域字體採用宙黑體

Soraeru／空耳聯合國的說明書、企劃、規格、簡報與 PDF 交付檔，**全域**使用開源 **宙黑體（Zev Hei TC）**；預設族名 `ZevHeiTC-N`。App UI 字體不在本決策範圍。

**Status**: accepted  
**Decided in**: 2026-11-27  
**Related**: `docs/fonts/README.md`

## Considered Options

- **宙黑體 Zev Hei TC，預設 N 版（選定）**：OFL-1.1 可商用、可嵌入；繁體為主、含簡體與西文（Mona Sans）；可變字重／字寬適合標題與內文同一家族。官方來源 [chiron-fonts/zev-hei-tc](https://github.com/chiron-fonts/zev-hei-tc)。
- **昭源黑體／思源黑體**：字形較傳統；宙黑體已在此基礎上拔腳、磨平尖角，更適合現代文件。
- **Noto Sans TC／微軟正黑體**：系統常見但不是專案可版本鎖定的交付字。
- **改 App 的 OpenSans**：與「生產文件」無關；MAUI UI 維持現況。

## Decision

1. 生產文件專用字體目錄為 `docs/fonts/zev-hei-tc/`，隨 repo 版本鎖定 `.otf` 與 OFL 授權檔。
2. 預設 `ZevHeiTC-N`、正文 400、標題 700；`ZevHeiTC-F` 僅標題需要底腳時使用。
3. 交付 PDF／docx 應嵌入字型。不得單獨販售字體檔。
4. Agent 產出生產文件時走 `.cursor/skills/soraeru-production-docs-font`。

## Consequences

- 倉庫約增加 65 MB（兩支 OTF）。TTF／WOFF2 不進版控。
- 編輯 Word／PPT 前須本機安裝（`scripts/install-zev-hei-tc-font.ps1`）。
- 字體版本 0.008，上游仍可能改名或改軸；更新時改 `NOTICE.md` 雜湊與 commit。
