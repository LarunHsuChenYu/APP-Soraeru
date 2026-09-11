# 開發建置／NuGet 路徑（Windows）

若 Visual Studio 出現 **Import Project 路徑超過 260 字元**，或錯誤路徑含 `cursor-sandbox-cache`，代表 NuGet 曾還原到 Cursor sandbox 長路徑，並寫進 `obj\*.nuget.g.targets`。這與 Android 部署「錯誤 22」**常同源**（sandbox 長路徑 → `obj` 污染／並行搶鎖 → 建置或部署失敗），處理方式同樣是短路徑還原＋清 `bin`／`obj`。

## 一勞永逸（repo 已硬化）

本 repo 用三層強制短路徑，讓本機／VS／`dotnet` 建置都優先走短路徑，即使程序曾注入 sandbox 的 `NUGET_PACKAGES`：

| 機制 | 作用 |
|---|---|
| `Directory.Build.props` → `RestorePackagesPath`／`NuGetPackageRoot`／`NuGetPackageFolders` | **僅 Windows**：MSBuild 屬性壓過錯誤的 `NUGET_PACKAGES`，restore 寫入 `obj` 時用 `D:\.nuget\packages` |
| `nuget.config` | 只鎖定套件來源 `nuget.org`（**不再**寫 `globalPackagesFolder`，否則 Linux Docker restore 會因沒有 `D:\` 失敗） |
| `Directory.Build.targets` | 若仍偵測到 `cursor-sandbox` 則建置立刻失敗並提示修 |

本機（Windows）仍走短路徑 `D:\.nuget\packages`；Linux／Docker 走預設 `~/.nuget/packages`。

請先建立資料夾（只需一次）：

```powershell
New-Item -ItemType Directory -Force -Path D:\.nuget\packages
```

### 請勿做的事

- **不要**把使用者／系統環境變數 `NUGET_PACKAGES` 設成 `%TEMP%\cursor-sandbox-cache\...\nuget` 或任何 Temp 長路徑。
- Cursor Agent 程序內可能暫時注入 sandbox 路徑；有了 `Directory.Build.props` 後，**專案 restore／build 應仍寫短路徑**。若 VS 仍爆路徑，多半是舊的污染 `obj`，跑下方一鍵修復即可。

### 建議的日常流程

1. 平常直接用 VS 或 `dotnet build`（不必每次手動設 `NUGET_PACKAGES`）。
2. **Agent／外部工具 restore 後**，若 VS Import／MAX_PATH 爆了 → 關 VS，跑：
   ```powershell
   .\scripts\fix-nuget-path.ps1 -Build
   ```
3. 重新開啟 `Soraeru.slnx`。

## 一鍵修復（obj 已污染時）

1. **關閉 Visual Studio**（避免鎖定 `obj`）。
2. 在**本機一般 PowerShell**（非 Cursor agent sandbox）於 repo 根目錄執行：
   ```powershell
   .\scripts\fix-nuget-path.ps1 -Build
   ```
   若其他專案的 `obj` 也被污染，加上 `-AllProjects`。
3. 重新開啟 `Soraeru.slnx` 再建置。

腳本會：清 `bin`／`obj`、取消錯誤的程序層 `NUGET_PACKAGES` 並設為 `D:\.nuget\packages`、`dotnet restore`（另帶 `-p:RestorePackagesPath`）、可選 windows TFM build，並驗證產物不含 `cursor-sandbox`。若偵測到**使用者／系統**層變數指到 sandbox，會警告你手動刪除（腳本不永久改機器設定）。

## 手動步驟

1. 關閉 Visual Studio（或至少卸載 solution）。
2. 刪除專案／solution 的 `bin`、`obj`（至少 `src/Soraeru.App/bin` 與 `obj`）。
3. 確認使用者／系統環境變數 **沒有** 把 `NUGET_PACKAGES` 指到 sandbox（見下一節）。
4. 在 repo 根目錄執行：
   ```powershell
   $env:NUGET_PACKAGES = "D:\.nuget\packages"   # 可選；props 已強制短路徑
   dotnet nuget locals global-packages -l       # 程序環境仍可能顯示 sandbox；以 obj 產物為準
   dotnet restore Soraeru.slnx
   ```
5. 重新用 VS 開啟 solution，或：
   ```powershell
   dotnet build src/Soraeru.App/Soraeru.App.csproj -f net10.0-windows10.0.19041.0
   ```

還原後的 `*.nuget.g.props` 應把 `NuGetPackageRoot` 設為 `D:\.nuget\packages\`，且不得含 `cursor-sandbox-cache`。

## 關於 NUGET_PACKAGES（含如何刪除使用者層變數）

Cursor Agent／sandbox 可能在**程序內**注入 `NUGET_PACKAGES` → `%TEMP%\cursor-sandbox-cache\...\nuget`。若曾被寫成**使用者層永久變數**，會讓「系統內容」裡的設定長期污染 VS 與一般終端。

### 檢查

PowerShell：

```powershell
[Environment]::GetEnvironmentVariable('NUGET_PACKAGES', 'User')
[Environment]::GetEnvironmentVariable('NUGET_PACKAGES', 'Machine')
$env:NUGET_PACKAGES   # 目前程序
```

若 User／Machine 顯示含 `cursor-sandbox` 或 Temp 長路徑，請刪除（不要設到 Temp）。

### 用 Windows 設定刪除（建議）

1. Win + R → `sysdm.cpl` → **進階** → **環境變數**。
2. 在「使用者變數」中找到 `NUGET_PACKAGES`。
3. 若值含 `cursor-sandbox-cache` 或過長 Temp 路徑 → **刪除**該變數（或改成 `D:\.nuget\packages`）。
4. 確定後**關閉並重開** Visual Studio、終端機、Cursor，使新環境生效。

### 用 PowerShell 刪除使用者層（可選）

```powershell
[Environment]::SetEnvironmentVariable('NUGET_PACKAGES', $null, 'User')
```

然後開新的終端／重啟 VS。不必也不建議改 git config；機器層（Machine）變數通常也不需要，優先只清使用者層。

有 `Directory.Build.props` 後，即使某次程序仍帶 sandbox 變數，**專案內 restore 仍應寫短路徑**；清掉永久變數則是避免工具鏈其它入口（例如 `dotnet nuget locals`、非 MSBuild 工具）繼續混亂。

## Windows 長路徑（可選）

可啟用 Windows「長路徑」群組原則／登錄，但 **MAUI／AndroidX 仍建議以短 `RestorePackagesPath`（`D:\.nuget\packages`）為主**，比只開長路徑更穩。

## Android 部署：「裝置無法辨識命令。(22)」

這是 Win32 `ERROR_BAD_COMMAND`（錯誤碼 22）的繁中訊息，常見於 Visual Studio 對模擬器／實機 **F5 部署**（Fast Deployment／ADB 同步）失敗，而不是 Google 登入邏輯錯誤。

**與 NuGet sandbox 路徑是否同源？** 部署錯誤 22 本身是 ADB／Fast Deployment 問題；但若先前 restore 把套件寫進超長 sandbox 路徑，AndroidX／MAUI 的 Import 與中介檔損壞，會讓「建置看似過、部署／後續步驟怪錯誤」更常一起出現。若同時有 `cursor-sandbox`／MAX_PATH／大量 Java `package ... does not exist`，先跑 `.\scripts\fix-nuget-path.ps1 -Build` 再查部署。

專案已在 Debug Android 設定 `EmbedAssembliesIntoApk=true`（關閉 Fast Deployment，改部署完整 APK）。若仍失敗，依序做：

1. 確認 VS 工具列目標為 **net10.0-android**，裝置選到 **emulator-xxxx（device）**，不要選離線／錯誤裝置。
2. 重新啟動 ADB（PowerShell）：
   ```powershell
   & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" kill-server
   & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" start-server
   & "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" devices -l
   ```
   應看到模擬器狀態為 `device`（不是 `offline`／`unauthorized`）。
3. 在模擬器／實機 **解除安裝** 既有 `空耳聯合國`（`com.soraeru.app`），再於 VS 按 F5。
4. 模擬器異常時：Device Manager → **Cold Boot**（或 Wipe Data 後冷啟動），等完全開機再部署。
5. 本機建置抽樣：
   ```powershell
   dotnet build src/Soraeru.App/Soraeru.App.csproj -f net10.0-android
   ```

仍失敗時請收集：VS「輸出」視窗（顯示來源選 Build／Xamarin／Deploy）完整錯誤列、`adb devices -l` 輸出、是否有多個 `adb.exe`。

## Android 大量 Java 錯誤：`package com.microsoft.maui does not exist`

若建置輸出出現例如：

- `package com.microsoft.maui does not exist`
- Glide / AndroidX 相關 `package ... does not exist`
- `cannot find symbol getClass()`（如 `ContentViewGroup`）

通常**不是業務程式碼錯誤**，而是 `obj` 中介損壞、與 Visual Studio **並行／設計時建置搶鎖**，或 NuGet 路徑不一致，導致 Java／aapt2 階段見到不完整的 library 匯入。

### 修復步驟（請先關閉 Visual Studio）

1. 關閉 Visual Studio（避免 `devenv`／MSBuild 鎖定或污染 `obj`）。
2. 優先一鍵修復：
   ```powershell
   .\scripts\fix-nuget-path.ps1 -AllProjects -Build
   ```
   或手動：
   ```powershell
   cd D:\VS\Soraeru
   $env:NUGET_PACKAGES = "D:\.nuget\packages"
   dotnet clean src/Soraeru.App/Soraeru.App.csproj -c Debug
   Remove-Item -Recurse -Force src\Soraeru.App\bin, src\Soraeru.App\obj -ErrorAction SilentlyContinue
   dotnet restore Soraeru.slnx
   dotnet build src/Soraeru.App/Soraeru.App.csproj -f net10.0-android -c Debug
   dotnet build src/Soraeru.App/Soraeru.App.csproj -f net10.0-windows10.0.19041.0 -c Debug
   ```
3. 再重新開啟 Visual Studio；啟動設定選 **net10.0-android** 後建置／部署。

### 在 VS 如何避免再發

- 清理重建前**先關 VS**，不要一邊開著方案一邊用 CLI／另一個 IDE 清 `obj`。
- 確認使用者／系統環境變數**沒有**把 `NUGET_PACKAGES` 指到 `cursor-sandbox-cache`。
- 避免同時對同一 App 專案跑多個建置（VS 設計時建置 + 外部 `dotnet build`）。
- 若同時出現 `APT2000`／「裝置無法辨識命令 (22)」與 zip `Permission denied`，優先視為 **檔案鎖定／並行污染**，同樣以關 VS → 刪 `obj` → 單執行緒重建處理。
