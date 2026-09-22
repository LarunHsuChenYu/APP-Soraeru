---
name: soraeru-dual-remote-push
description: >-
  When committing or pushing Soraeru git changes, always push the same branch to
  both GitHub remotes (personal LarunHsuChenYu and company Soraeru-Development).
  Use whenever the user asks to commit, push, sync GitHub, or deploy via Railway
  from this repo.
---

# Soraeru 雙遠端 Git Push

## 原則（verbatim）

之後 commit/push 都要到這兩個地方

1. https://github.com/LarunHsuChenYu/APP-Soraeru  
   （個人用）

2. https://github.com/Soraeru-Development/APP-Soraeru  
   （Ash-Ben軟體科技公司使用）

## Railway 帳號（verbatim）

目前推上的 Railway 的帳號為 asbe2026@gmail.com  
此為公司帳號。

- 部署／Dashboard／Variables／Volume 操作預設對應此公司 Railway 帳號
- 勿與個人 Railway／個人 GitHub 部署環境混淆
- GitHub 自動部署來源應對齊公司 repo：`Soraeru-Development/APP-Soraeru`

## When to apply

- 使用者要求 **commit**、**push**、同步 GitHub、或為 Railway 部署推送程式
- Agent 完成實作後若被要求上庫，一律雙推

## Remotes（本機慣例）

| 用途 | 建議 remote 名 | URL |
|------|----------------|-----|
| 個人 | `personal` 或 `origin`（fetch） | `https://github.com/LarunHsuChenYu/APP-Soraeru.git` |
| 公司 | `github` | `https://github.com/Soraeru-Development/APP-Soraeru.git` |

若 `origin` 已設定 **兩個 push URL**（個人＋公司），`git push origin HEAD` 可一次推兩邊；仍須確認兩邊都成功。

## Required push sequence

在 **commit 成功之後**（且使用者有要求 push）：

```powershell
# 確認 remote
git remote -v

# 推目前分支到個人與公司（替換 <branch> 為實際分支名，或用 HEAD）
git push personal HEAD
git push github HEAD
```

若 `personal`／`github` 不存在，先加：

```powershell
git remote add personal https://github.com/LarunHsuChenYu/APP-Soraeru.git
git remote add github https://github.com/Soraeru-Development/APP-Soraeru.git
```

或等價：

```powershell
git push https://github.com/LarunHsuChenYu/APP-Soraeru.git HEAD
git push https://github.com/Soraeru-Development/APP-Soraeru.git HEAD
```

## 完成條件

- [ ] 兩個 remote 的 push 都 exit 0（或明確回報哪一邊失敗與原因）
- [ ] `git status -sb` 顯示與預期追蹤一致
- [ ] 回覆使用者時列出兩個 repo URL／分支名

## 不要做的事

- 不要只推其中一個就宣稱「已同步」
- 不要 `push --force` 到 `main`／`master`，除非使用者明確要求
- 不要把 `.tmp-*`、金鑰、APK 二進位當預設 commit 內容
