# ADR 是什麼？

**ADR**＝**Architecture Decision Record**（架構決策紀錄）。

它不是產品功能，而是一篇很短的筆記，用來記下：

- 當時選了哪條路  
- 有哪些替代方案被排除  
- **為什麼**這樣選  

之後換聊天、換人或隔幾個月，不必靠記憶猜「當初為何不用允許清單／為何要獨立策展站」。

## 本目錄慣例

| 項目 | 說明 |
|------|------|
| 檔名 | `NNNN-短英文 slug.md`（編號遞增） |
| Status | `proposed` → `accepted`；被取代時改 `superseded by ADR-NNNN` |
| 內容 | 背景＋決定＋取捨；夠短即可 |

現有：`0001`…`0012`；LLM 執行期設定／用量見 **`0013-llm-runtime-settings-sqlite.md`**。產品領域詞見 `../glossary.md`。
