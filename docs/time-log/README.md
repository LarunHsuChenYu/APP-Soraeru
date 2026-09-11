# 工時帳（Soraeru MVP）

輕量工時紀錄，給 **1～2 人**（Ashton + 協作者）使用。不接 Jira／不強制流程；每天收工花 **1 分鐘** 填一列即可。

規劃粗估（見 Cursor-MVP App 規劃書）：約 **300～420 小時**。

## 累計快照（截至 2026-09-10；含 08-31～09-10 backfill＋午後 App 登入統計）

| 項目 | 值 |
|---|---|
| 起始有證據日 | **2026-08-04** |
| 已用小時 | **94.0** |
| 已用人天（÷8） | **≈ 11.8 人天** |
| 剩餘（低標 300h） | ≈ **206 h** |
| 剩餘（高標 420h） | ≈ **326 h** |
| 進度％（中位 360h） | ≈ **26.1%** |
| 記入人員 | 僅 **Ashton**（無第二人證據） |

## 檔案

| 檔案 | 用途 |
|---|---|
| [`timesheet.csv`](timesheet.csv) | 總表（建議主帳；Excel／Google 試算表可直接開） |
| `YYYY-Www.md` | 當週備註／當日摘要（可選；與 CSV 同步也可） |

## 欄位

| 欄位 | 說明 |
|---|---|
| `date` | 工作日 `YYYY-MM-DD` |
| `person` | `Ashton` 或協作者名稱 |
| `hours` | 小數小時即可（例如 `1.5`、`3`） |
| `ticket` | 票號：`01`…`17`、或 `plan`／`setup`／`auth`／`prompt`／`planning`／`tickets`／`review`／`misc` |
| `note` | 一句話（做了什麼） |
| `kind` | `dev`／`plan`／`ops`／`meeting`／`backfill`（估計補登） |

## 每日習慣（建議）

1. **收工前**在 `timesheet.csv` 加一列（或週五一次補齊當週也行，但越當日越準）。
2. 票號對齊 [`docs/tickets/README.md`](../tickets/README.md)（例如 OCR＝`07`、TTS＝`08`）。
3. 會議／規劃／環境若無票，用 `plan`／`setup`／`misc`，不要硬塞。
4. 週一開新週檔 `YYYY-Www.md`（ISO 週；可複製上一週骨架）。

## 對照計畫用量

```text
已用人天 = SUM(hours) / 8
已用小時 = SUM(hours)
剩餘（低標） = 300 − 已用小時
剩餘（高標） = 420 − 已用小時
進度％（中位 360h）≈ 已用小時 / 360
```

在 Google Sheet／Excel：對 `hours` 欄 `SUM`，再除以 8 得人天。

## 回溯補登方法（2026-08-11 執行）

**原則**：只記 **Ashton 與 AI 協作時的日曆在席時間**；AI 寫碼不另開 `person=agent` 列，避免把 wall-clock 與自動編碼二次加總。`kind=backfill`＝估計；**寧可低估**。

| 來源 | 用法 |
|---|---|
| Cursor agent transcripts（主線 `99782efb…`＋同日兄弟 session） | 使用者訊息 `<timestamp>`（UTC+8）估連續在席區間 |
| Subagent 檔案建立／寫入時間 | 佐證某段工作存在，不單獨加長工時 |
| `docs/**`／`src/**` CreationTime | 佐證 8/4–6 規劃與源碼起點；**不**用檔案數線性灌鐘 |
| `git log` | main 尚無 commit → 無助於日期 |
| 8/8–8/9 | 無 transcript、無 docs 新建 → **0h** |

### 日曆在席（transcript 高信心）

| 日 | 大約時段（UTC+8） | 保守登入 |
|---|---|---|
| 2026-08-07 | ≈10:07–17:54（午休／空隙已扣） | 6.5h |
| 2026-08-10 | ≈09:20–17:42（午休已扣） | 7.5h |
| 2026-08-11 | 上午回溯＋票13／規劃 6.0h；午後 Stitch UI＋setup 6.0h（見 CSV） | 12.0h |

### 檔案證據日（中低信心，低估）

| 日 | 證據摘要 | 保守登入 |
|---|---|---|
| 2026-08-04 | Claude／ChatGPT MVP 草稿 | 2h |
| 2026-08-05 | Gemini MVP 規劃 | 1.5h |
| 2026-08-06 | Cursor-MVP／Stitch／簡報／App 骨架 | 5h |

**信心 caveat**：8/4–6 無 Cursor 時間戳，可能漏非對話工作；互動日也可能漏「離開螢幕思考／實機折騰」或高估「掛著對話」。數字適合對照 300–420h 曲線，不宜當精確薪資帳。

## 三選一（選用）

### Option 1 — 本目錄 Markdown／CSV（預設，已建好）

優點：跟 repo 同一處、可 diff、零帳號。缺點：協作需自己同步（或之後進 git）。

### Option 2 — Google Sheet

同一欄位表頭：`date, person, hours, ticket, note, kind`。  
一人建表 → 分享編輯給第二人；每週把 Sheet 匯出 CSV 覆寫 `timesheet.csv`（可選）。

### Option 3 — Commit message 記工時（不建議當主帳）

慣例範例：`feat(08): TTS 正式發音 [2.5h]`  

| 優點 | 缺點 |
|---|---|
| 跟變更連在一起 | 本 repo 目前尚無 commit；且規劃／會議無 commit 會漏 |
| 事後可從 log 掃 | 多人／平行分支難匯總；容易灌水或漏填 |

可當**輔助**，正式統計仍以 CSV／Sheet 為準。

## 起始日期證據摘要（供補登參考）

| 來源 | 結果 |
|---|---|
| `git log` | repo 有 `.git`，但 **main 尚無任何 commit** → 無 commit 日期 |
| 檔案建立時間（ctime） | 最早約 **2026-08-04**（MVP 規劃書草稿） |
| 規劃書內建日期 | 多數標 **2026-08-06**（含 Cursor-MVP App 規劃書） |
| 源碼樹出現 | App ≈ **2026-08-06**；Api／Application／Infrastructure ≈ **2026-08-07** |
| Cursor 互動 | **8/7、8/10、8/11**（主線自 8/7 10:07 起） |
| 「上周」（相對 2026-08-11） | **8/3–8/9** → 可見痕跡落在上周，非更早 |

## 範例列

見 `timesheet.csv` 與 `2026-W32.md`／`2026-W33.md`／`2026-W34.md`／`2026-W35.md`／`2026-W37.md`。
