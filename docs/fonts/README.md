# 生產文件專用字體：宙黑體

Soraeru／空耳聯合國對外與對內的 **生產文件**（說明書、企劃、規格、簡報、合約／法務稿、PDF 交付檔）**全域**使用 **宙黑體（Zev Hei TC）**。

App UI 仍用既有 MAUI 字（`OpenSans*`），**不要**把本字體改成 App 預設。

## 預設

| 用途 | 字族 | 字重 |
|------|------|------|
| 正文、表格、圖說 | `ZevHeiTC-N` | 400 Regular |
| 標題、章名 | `ZevHeiTC-N` | 700 Bold |
| 次標題 | `ZevHeiTC-N` | 500–600 |
| 極細裝飾（少用） | `ZevHeiTC-N` | 200–300 |
| 需要底部承托腳的標題 | `ZevHeiTC-F` | 同左 |

N 版＝完全拔腳（上游指定為 Zevacode 基礎字形），作為全域預設。F 版僅在標題想保留底腳時改用。

軟體字體選單請搜尋 **`ZevHeiTC-N`**。部分應用程式的粗體按鈕對應 700，不會另外出現 Bold 項目。

## 檔案位置

```text
docs/fonts/zev-hei-tc/ZevHeiTC-N-VF.otf   ← 預設
docs/fonts/zev-hei-tc/ZevHeiTC-F-VF.otf
docs/fonts/zev-hei-tc/LICENSE.md
docs/fonts/zev-hei-tc.css                 ← HTML／PDF 管線
docs/fonts/specimen.html                  ← 樣張
docs/fonts/specimen.png                   ← 樣張截圖
```

授權與來源見 [`zev-hei-tc/NOTICE.md`](zev-hei-tc/NOTICE.md)。決策見 [ADR-0014](../adr/0014-zev-hei-tc-production-docs-font.md)。

## 本機安裝（Word／PowerPoint／Excel）

Windows（建議用目前使用者字型，不必系統管理員）：

```powershell
.\scripts\install-zev-hei-tc-font.ps1
```

安裝後**重開 Office**，字體選單搜尋 `ZevHeiTC-N`。

macOS：將兩個 `.otf` 雙擊裝進「字體冊」。Linux：複製到 `~/.local/share/fonts/` 後執行 `fc-cache -f`。

## HTML／Markdown→PDF

```html
<link rel="stylesheet" href="docs/fonts/zev-hei-tc.css">
```

```css
body { font-family: "ZevHeiTC-N", sans-serif; font-weight: 400; }
h1, h2, h3 { font-family: "ZevHeiTC-N", sans-serif; font-weight: 700; }
```

可變字型請寫 `font-weight: 400`，不要只靠家族預設（檔案內 fvar 預設軸是 200 ExtraLight）。

## Word／python-docx

中英混排須同時設 Latin 與 East Asian 字族：

```python
from docx.oxml.ns import qn
run.font.name = "ZevHeiTC-N"
run.font.bold = is_heading  # 標題 True → 700
run._element.rPr.rFonts.set(qn("w:eastAsia"), "ZevHeiTC-N")
```

產出檔請嵌字（Word：檔案 → 選項 → 儲存 → 將字型嵌入檔案），讓沒安裝字型的收件者仍能看到正確字形。
