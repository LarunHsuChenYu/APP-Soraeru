# 開發環境：Google 登入（OAuth / idToken）

本文件說明如何在本機完成 **Android Google Sign-In → API 驗證 idToken → 發 Soraeru JWT** 的設定與驗證。

相關 API：`POST /api/v1/auth/google`（body：`{ "idToken": "..." }`）。

## 1. 概念（必讀）

| 項目 | 用途 |
|---|---|
| **Web Client ID** | App `RequestIdToken(webClientId)` 與後端 `GoogleAuth:ClientIds` 的 **主要 audience** |
| **Android Client ID** | 綁定 package `com.soraeru.app` + SHA-1；可一併放進 `ClientIds` 當有效 audience |
| **idToken** | 給**自家後端**驗證用（不是 accessToken） |

常見錯誤：把 Android Client ID 傳給 `RequestIdToken` → 拿不到可用的 server idToken。

## 2. Google Cloud Console 步驟

1. 開啟 [Google Cloud Console](https://console.cloud.google.com/) → 建立或選擇專案。
2. **API 和服務** → **OAuth 同意畫面**：選 External（或 Internal），填應用程式名稱，至少加入測試使用者（開發者 Gmail）：
   - `larun70@gmail.com`
   - `avai.hsu@gmail.com`
3. **憑證** → **建立憑證** → **OAuth 用戶端 ID**：
   - 類型 **網頁應用程式** → 記下 **用戶端 ID**（Web）。
   - 類型 **Android**：
     - 套件名稱：`com.soraeru.app`（與 `Soraeru.App.csproj` 的 `ApplicationId` 一致）
     - SHA-1：見下一節 `keytool`
     - 記下 Android 用戶端 ID（建議也加入後端 `ClientIds`）

## 3. 取得 Android SHA-1（debug）

> **MAUI / Visual Studio 重要：** Debug APK **不是**用 `%USERPROFILE%\.android\debug.keystore` 簽名，而是用：
>
> `%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`
>
> 兩個 keystore 的 SHA-1 **通常不同**。Console 若只登錄 `.android` 那把，會出現 `DEVELOPER_ERROR / 10`。

### 3.1 以實際 Signed APK 為準（最可靠）

建置後：

```powershell
keytool -printcert -jarfile "src\Soraeru.App\bin\Debug\net10.0-android\com.soraeru.app-Signed.apk"
```

複製輸出的 `SHA1:` 一行，登錄到 Google Cloud **Android** OAuth 用戶端（package=`com.soraeru.app`）。

### 3.2 MAUI / Xamarin debug keystore（與 Signed APK 應一致）

```powershell
keytool -list -v -keystore "$env:LOCALAPPDATA\Xamarin\Mono for Android\debug.keystore" -alias androiddebugkey -storepass android -keypass android
```

本機（`ashtonhsu`）MAUI debug **SHA-1**（與 `com.soraeru.app-Signed.apk` 一致）：

```text
B4:91:3D:E3:CA:BB:02:52:CA:F3:6F:5C:BD:63:C7:64:2E:21:A8:69
```

### 3.3 Android Studio 預設 debug keystore（MAUI 通常不用這把簽 APK）

```powershell
keytool -list -v -keystore "$env:USERPROFILE\.android\debug.keystore" -alias androiddebugkey -storepass android -keypass android
```

同機 `.android` debug **SHA-1**（**勿**當成 MAUI 唯一登錄值）：

```text
C4:67:1B:33:7D:B7:C0:89:12:7F:6E:2A:88:AA:CE:E2:D2:3F:BA:96
```

若 keystore 尚不存在，先用 Android Studio／先跑一次 `dotnet build -t:Run -f net10.0-android` 產生。

換電腦或更換簽名 APK 時，SHA-1 會變；需更新 Console 或另開用戶端。

## 4. 後端設定（API）

### 4.1 範例檔（可提交）

`src/Soraeru.Api/appsettings.Development.json`：

```json
"GoogleAuth": {
  "ClientIds": [
    "REPLACE_WITH_WEB_CLIENT_ID.apps.googleusercontent.com",
    "REPLACE_WITH_ANDROID_CLIENT_ID.apps.googleusercontent.com"
  ]
}
```

含 `REPLACE_WITH` 的佔位字串**不會**被當成有效設定；未替換時 API 回：

- HTTP **503**
- code：`GOOGLE_AUTH_NOT_CONFIGURED`

正式 `appsettings.json` 預設 `ClientIds: []`，勿提交真實 client id／secret。

### 4.2 建議：User Secrets（本機真實值）

```powershell
cd src/Soraeru.Api
dotnet user-secrets init
dotnet user-secrets set "GoogleAuth:ClientIds:0" "你的WebClientId.apps.googleusercontent.com"
dotnet user-secrets set "GoogleAuth:ClientIds:1" "你的AndroidClientId.apps.googleusercontent.com"
```

或環境變數（雙底線）：

```text
GoogleAuth__ClientIds__0=....apps.googleusercontent.com
GoogleAuth__ClientIds__1=....apps.googleusercontent.com
```

後端用 `Google.Apis.Auth` 的 `GoogleJsonWebSignature.ValidateAsync`，audience 必須落在 `ClientIds` 內。

## 5. App（Android）設定

**建議（本機密鑰，不進 git）**：複製範例後填入 **Web** Client ID（不要填 Android Client ID）：

```powershell
cd src/Soraeru.App
copy GoogleAuth.Debug.json.example GoogleAuth.Debug.json
# 編輯 GoogleAuth.Debug.json：
# { "WebClientId": "你的WebClientId.apps.googleusercontent.com" }
```

`GoogleAuth.Debug.json` 已列入 `.gitignore`；存在時會以 EmbeddedResource 嵌入 Debug 建置，由 `GoogleAuthClientIds.ResolveWebClientId()` 讀取。未設定時 App 會提示「尚未設定 Google Web Client ID」。

套件：`Xamarin.GooglePlayServices.Auth`（僅 Android TFM）。另在 csproj 明確對齊 AndroidX `Activity.Ktx`／`Fragment.Ktx`／`Lifecycle.*`（2.11／1.13／1.8.9），避免與 MAUI 傳遞相依的舊版 `*.Ktx` 嚴格上限衝突。

**minSdk**：`SupportedOSPlatformVersion`（Android）為 **23**（Android 6.0）。因 `androidx.lifecycle.runtime`／GMS Auth 宣告最低 23；產品規劃仍為 Android 8.0+，此處只是綁定底線。

Package 名：`com.soraeru.app`。

API BaseAddress：Debug 模擬器 `http://10.0.2.2:5080/`；Android Release／封閉測試 APK 為 `https://soraeru.ash-ben.com/`（見 `MauiProgram.ResolveApiBaseUrl`）。實機 Debug 可定義 `USE_RAILWAY_API` 或改 LAN IP。

## 6. Windows vs Android

| 平台 | 行為 |
|---|---|
| **Android** | 按「使用 Google 登入」→ Play Services 帳號選擇 → 取得 idToken → `POST /auth/google` → 寫入 `IAuthSessionStore` → 依 `OnboardingCompleted` 進 L04／L05 |
| **Windows** | 按鈕顯示提示「請在 Android 上使用 Google 登入」，**不崩潰**（`UnsupportedGoogleSignInService`） |

Email 登入／註冊在兩平台皆可用。

## 7. 綁定規則（後端）

1. 驗證 idToken → 取得 `sub`、email、name；**無 email 則失敗**（`GOOGLE_EMAIL_REQUIRED`）。
2. `GoogleSubject == sub` → 登入該使用者。
3. 否則同 Email（忽略大小寫）：
   - 已存在 → **綁定** `GoogleSubject`（DisplayName 為空時可用 Google name）。
   - 不存在 → **新建**（`PasswordHash` 可空，Free 額度與 Email 註冊一致）。
4. 開發者 allowlist（`larun70@gmail.com`、`avai.hsu@gmail.com`）登入／建帳後 `IsDeveloper=true`、額度無限。
5. Email 註冊撞到「純 Google 帳（無密碼）」→ `EMAIL_TAKEN_GOOGLE`，提示改用 Google 登入。

## 8. 如何驗證

### 8.1 後端未設定 ClientIds

```powershell
dotnet run --project src/Soraeru.Api
curl -s -X POST http://localhost:5080/api/v1/auth/google -H "Content-Type: application/json" -d "{\"idToken\":\"x\"}"
```

預期：503 + `GOOGLE_AUTH_NOT_CONFIGURED`。

### 8.2 Android 模擬器／實機（端到端）

1. 設定好 User Secrets + App `GoogleAuth.Debug.json`（Web Client ID）。
2. 啟動 API（埠 **5080**）。
3. 以 Android 目標執行 App。
4. L01 點「使用 Google 登入」，選 `larun70@gmail.com` 或 `avai.hsu@gmail.com`。
5. 首次應進 Onboarding（L04）；按「開始使用」會 `PATCH /api/v1/me` 寫入 `onboardingCompleted=true`。
   - 之後冷啟動若本機 Session 仍有效，Splash 會略過登入頁；要重看登入請先到設定「登出」。
6. 設定頁登入方式應顯示 **Google**（若之後再綁密碼則 **Google／Email**）。
7. 首頁額度對開發者應顯示 **無限制**（讀 `/me`）。
8. 再以同一 Google 登入 → 不建第二帳；`Users.GoogleSubject` 維持同一個 `sub`。

### 8.3 Email ↔ Google 綁定

1. 用 Email 註冊 `test@example.com`。
2. 用同一個 Gmail（若測試帳剛好同 mail）走 Google → 應**綁定**而非新建列。
3. 純 Google 帳再走 Email 註冊同 mail → 明確衝突訊息。

## 9. 疑難排解

| 現象 | 檢查 |
|---|---|
| `GOOGLE_AUTH_NOT_CONFIGURED` | User Secrets／appsettings 的 `ClientIds` 是否已換成真實值 |
| `GOOGLE_TOKEN_INVALID` | Web Client ID 是否與 `RequestIdToken` 一致；token 是否過期 |
| Android 拿不到 idToken | `WebClientId` 是否誤填 Android Client ID；SHA-1／package 是否匹配 |
| 一律「已取消 Google 登入」 | **先重裝／重跑含錯誤對應的建置**；真正取消為 status `12501`。若改為 `DEVELOPER_ERROR / 10` → **請用 Signed APK / Xamarin Mono for Android keystore 的 SHA-1**（非 .android）／package；`SIGN_IN_REQUIRED / 4` → 裝置未登 Google；`API_NOT_CONNECTED` → 模擬器無 Google Play；`SIGN_IN_FAILED / 12500` → OAuth 測試使用者／Play 映像 |
| 模擬器彈帳號選擇器後立刻失敗 | 使用 **帶 Google Play** 的 AVD；在模擬器設定登入開發用 Gmail；OAuth 同意畫面 External 時把該 Gmail 加進測試使用者 |
| 模擬器連不到 API | API 是否聽 `0.0.0.0`／`localhost:5080`；模擬器用 `10.0.2.2` |
| DLL 被鎖定 | 停止先前 `Soraeru.Api` 再 build（見 `docs/dev-setup-db.md`） |
| `obj\...\lp\...` 檔案被占用 | 停止 VS 建置／關閉占用的 MSBuild、VBCSCompiler；`dotnet clean` 後刪 `src/Soraeru.App/bin` 與 `obj` 再建 |
| `minSdkVersion 21 cannot be smaller than 23` | 確認 `Soraeru.App.csproj` Android `SupportedOSPlatformVersion` 為 `23.0` |
| AndroidX「超出約束」／LiveData／Fragment.Ktx | 確認 csproj 內對齊的 AndroidX PackageReference 未被移除；`dotnet restore` 後再建 |
| Android `APT2000` 打包失敗 | 多為本機 aapt2／路徑環境問題；先確認 `dotnet build -f net10.0-android -t:CoreCompile` 通過。可清 `obj` 後以 Visual Studio 建 Android，或更新 Android SDK Build-Tools |
| GMS namespace duplicate 警告 | `com.google.android.gms.auth.api*` 重複宣告屬常見警告，通常可忽略 |


### 9.1 `DEVELOPER_ERROR / 10` 排查（Android）

1. **package**：`ApplicationId` 必須是 `com.soraeru.app`（與 Android OAuth 用戶端一致）。
2. **WebClientId**：`GoogleAuth.Debug.json` 必須是 **Web** Client ID（`…4qvopv54…`），不可填 Android Client ID。
3. **SHA-1**：不要只查 `%USERPROFILE%\.android\debug.keystore`。請用：
   - `keytool -printcert -jarfile ...\com.soraeru.app-Signed.apk`，或
   - `%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`
4. 在 Google Cloud → **API 和服務** → **憑證** → 開啟（或新建）**Android** OAuth 用戶端 → 套件名稱 `com.soraeru.app` → 指紋填入 Signed APK 的 SHA-1 → 儲存。
5. 等 5～10 分鐘後，**卸載**模擬器／裝置上的 App 再重裝；確認 OAuth 同意畫面已把測試 Gmail 加為測試使用者。
