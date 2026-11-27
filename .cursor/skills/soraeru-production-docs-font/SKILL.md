---
name: soraeru-production-docs-font
description: >-
  Use 宙黑體 (ZevHeiTC-N) as the global typeface for Soraeru production
  documents: manuals, specs, proposals, slide decks, legal drafts, PDF
  exports. Use when creating or restyling docx, pptx, pdf, HTML print
  docs, or 說明書／企劃／規格／簡報.
---

# Soraeru 生產文件字體

## 原則（verbatim）

下載「宙黑體」當作「全域」的各式各樣生產文件專用字體。

## When to apply

產出或改版 **生產文件**（說明書、企劃書、規格、簡報、法務稿、交付 PDF／HTML 列印稿）時。

**不要**用在 MAUI App UI（仍為 `OpenSansRegular`／`OpenSansSemibold`）。

## Typeface

| 角色 | 族名 | 字重 |
|------|------|------|
| 預設／正文 | `ZevHeiTC-N` | 400 |
| 標題 | `ZevHeiTC-N` | 700 |
| 底腳變體 | `ZevHeiTC-F` | 同左，僅標題需要時 |

檔案：

- `docs/fonts/zev-hei-tc/ZevHeiTC-N-VF.otf`
- `docs/fonts/zev-hei-tc/ZevHeiTC-F-VF.otf`
- HTML：`docs/fonts/zev-hei-tc.css`
- 說明：`docs/fonts/README.md`

Word 中英混排須設 East Asian 字族為 `ZevHeiTC-N`。HTML 必須寫 `font-weight: 400`（可變字型 fvar 預設軸是 200）。交付檔嵌入字型。授權 OFL-1.1，不得單獨賣字體檔。
