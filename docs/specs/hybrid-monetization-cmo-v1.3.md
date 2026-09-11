> **Superseded：** 本檔為 CMO v1.3 歷史參考；**實作與對外溝通以 v2.0 整合版為準** → `docs/specs/hybrid-monetization-integrated.md`

---

# **Soraeru / 空耳聯合國 \- 混合獲利模式與系統整合企劃書**

## **(每日免費配額 \+ 獎勵廣告贈點 \+ NT$ 129/月 VIP 訂閱)**

**文件版本：** v1.3

**核心定位：** 輕量無痛入門、高廣告毛利支撐、高轉換 VIP 訂閱

**核心團隊：**

* **Aeshton (CTO / 技術總監)：** 額度中介軟體、AdMob Server-Side Verification (SSV)、TapPay 定期扣款、資料庫交易一致性。  
* **Ben (CMO / 行銷總監)：** AdMob 廣告單元配置、訂閱彈窗與缺額度引導 UI/UX 設計、短影音行銷與付費轉換漏斗優化。

## **1\. 核心獲利架構與設計理念 (Executive Summary)**

為了在「零門檻獲客 (User Acquisition)」、「覆蓋 API 運算成本 (Cost Recovery)」與「建立穩定被動經常性收入 (ARR)」之間取得最佳平衡，Soraeru 採用 **「三階梯混合漏斗 (Three-Tier Hybrid Funnel)」**：

               ┌───────────────────────────────────────────────┐  
               │              Soraeru 使用者分流漏斗             │  
               └───────────────────────┬───────────────────────┘  
                                       │  
         ┌─────────────────────────────┼─────────────────────────────┐  
         ▼                             ▼                             ▼  
  【1. 免費用戶 (Free)】      【2. 輕度活躍 (Ad-Supported)】    【3. VIP 訂閱者 (Pro)】  
  • 每日重置 5 次免費分析       • 看 1 則 15-30s 獎勵廣告       • 固定月費 NT$ 129 / 月  
  • 基礎單字卡儲存             • 立即獲得 \+2 次分析額度        • 無限次 AI 深度解析  
  • 目標：體驗魔法、降低門檻   • 每日上限看 3 次 (+6 次)       • 零廣告、語音連播、雲端備份  
                               • 目標：廣告變現、覆蓋 API 成本 • 目標：高 LTV 穩定現金流

## **3\. 三階梯混合獲利模型 (Hybrid Monetization)**

Soraeru 採用 **「每日免費配額 \+ 看廣告贈點 \+ NT$ 129/月 VIP 訂閱」** 的三階梯混合漏斗：

Plaintext  
               ┌───────────────────────────────────────────────┐  
               │              Soraeru 使用者分流漏斗             │  
               └───────────────────────┬───────────────────────┘  
                                       │  
         ┌─────────────────────────────┼─────────────────────────────┐  
         ▼                             ▼                             ▼  
  【1. 免費用戶 (Free)】      【2. 輕度活躍 (Ad-Supported)】    【3. VIP 訂閱者 (Pro)】  
  • 每日重置 5 次免費分析       • 看 1 則 15-30s 獎勵廣告       • 固定月費 NT$ 129 / 月  
  • 基礎單字卡儲存 (上限30張)   • 立即獲得 \+2 次分析額度        • 無限次 AI 深度解析  
  • 目標：體驗魔法、降低門檻   • 每日上限看 3 次 (+6 次)       • 零廣告、語音連播、雲端備份  
                               • 目標：廣告變現、覆蓋 API 成本 • 目標：高 LTV 穩定現金流

### **3.1 方案規則矩陣**

| 方案類別 | 每日基礎額度 | 廣告獲取額度 (Ad Bonus) | 每日最大使用上限 | 廣告體驗 | 單字庫上限 | 費用 (TWD) |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| **免費會員 (Free Tier)** | **5 次** / 日 (午夜重置) | 無 | 5 次 | 無插頁廣告 (乾淨體驗) | 30 張卡片 | **NT$ 0** |
| **廣告支持 (Ad-Supported)** | 5 次 (基礎) | **每次完整觀看 \+2 次** (每日上限看 3 次廣告) | **11 次** / 日 | 僅主動點擊觀看獎勵廣告 (Rewarded Video) | 50 張卡片 | **NT$ 0** (廣告換取) |
| **Pro VIP 訂閱會員** | **無限次** (防刷軟上限 300次) | 免看廣告 | 300 次 / 日 | **100% 零廣告** | 無上限 | **NT$ 129** / 月 |

### **3.2 單位經濟模型與財務可行性試算 (Unit Economics)**

* **Gemini 1.5 Flash API 成本：** 每筆分析平均消耗約 300 tokens，單次成本約 **NT$ 0.001 TWD**。  
* **Google AdMob 台灣獎勵影音廣告 (Rewarded Video) eCPM：** 約 **US$ 15.00 \~ 25.00**，單次觀看收益約 **NT$ 0.48 \~ 0.80 TWD**（取平均 NT$ 0.60）。  
* **TapPay 網頁 Google Pay 手續費：** **2.8%**（每筆 NT$ 129 扣手續費 NT$ 3.61，實收 **NT$ 125.39 TWD**）。

#### **各族群毛利率試算：**

> 1. **免費用戶（用滿 5 次）：** 每日成本 $5 \\times 0.001 \= \\text{NT\\$ } 0.005$。單月成本僅 NT$ 0.15，獲客與口碑門檻極低。  
> 2. **看廣告用戶（每日用滿 11 次 \= 基礎 5 次 \+ 觀看 3 次廣告獲 6 次）：**  
   * 每日成本：$11 \\times 0.001 \= \\text{NT\\$ } 0.011$  
   * 每日收益：$3 \\times 0.60 \= \\text{NT\\$ } 1.80$  
   * **每日淨利：** $\\text{NT\\$ } 1.80 \- \\text{NT\\$ } 0.011 \= \+\\text{NT\\$ } 1.789$（**毛利率達 99.3%**）  
> 3. **Pro VIP 用戶 (NT$ 129 / 月)：**  
   * 預估重度用戶每月分析 450 次，API 成本約 NT$ 0.45，金流費 NT$ 3.61。  
   * **每月淨利：** $129 \- 3.61 \- 0.45 \= \\text{NT\\$ } 124.94$（**毛利率達 96.8%**）

### **3.3 網頁版 Google Pay 金流策略 (Web Checkout Portal)**

* **避開平台抽成：** 依據 Google 政策，App 內引導使用者至官方網頁 (\[https://pay.soraeru.com\](https://pay.soraeru.com)) 結帳。  
* **金流服務商 (PSP)：** 串接 **TapPay (喬睿科技)** 之 Google Pay Direct Pay 模式，手續費僅 **2.4% \~ 2.8%**。  
* **自動續訂 (Recurring Billing)：** 使用者於網頁首訂後，後端安全保存 CardToken，每月由排程服務自動發起 TapPay pay-by-token 請款。

### **3.4 雙向推薦獎勵機制 (Referral Program)**

* 每位用戶擁有專屬邀請碼。成功邀請好友註冊並登入，雙方皆可獲得 3 天 Pro 體驗或額外分析額度，大幅降低獲客成本 ($CAC$)。

## **4\. 行銷推廣與異業合作案 (Marketing & Partnerships)**

### **4.1 階段性推廣時程 (Marketing Phases)**

> 1. **Phase 1: 預熱與封測期 (W11-W14)**：於 Threads/Dcard 招募 12+ 位測試者參與 Google Play 14 天封閉測試；上線 Waitlist 著陸頁。  
> 2. **Phase 2: 上架爆發期 (W15-W18)**：發布 20-30 秒短影音 (Reels/Shorts/TikTok)，主題涵蓋「日本拉麵店痛扣此」、「韓劇阿婆洗碗」等爆笑情境；於 PTT Soft\_Job 發布兩人開發血淚史。  
> 3. **Phase 3: 社群擴散與 UGC (W19+)**：舉辦「最瞎空耳徵稿活動」，每月抽選最佳諧音大師贈送 Pro 會籍。

### **4.2 五大異業合作方案 (Cross-Industry Partnerships)**

* **方案一（海外網卡/eSIM）：** 與 DJB、威訊等網卡業者合作，買網卡隨附「Soraeru 出國生存包專屬開通碼」。  
* **方案二（自由行平台）：** 於 KKday/Klook 預訂特定海外行程頁面（如居酒屋體驗），隨附一鍵匯入相關單字包。  
* **方案三（追星應援社群）：** 與 K-POP 粉絲粉專合作，推出「偶像直播/演唱會應援詞庫包」。  
* **方案四（代標代購平台）：** 與比比昂、樂淘合作，提供「日本 Mercari 拍賣避坑防雷單字卡」。  
* **方案五（語言學習與創作者）：** 邀請旅遊/語言 KOL 建立「創作者專屬詞庫」，提供專屬分潤連結。

## **5\. 財務預測與營運預算 (Financials & Budget)**

### **5.1 前 5 個月現金預算規劃表**

| 預算項目 | 規格 / 服務商 | 估算金額 (TWD) | 說明 |
| :---- | :---- | :---- | :---- |
| **平台與規費** | Google Play 帳號 (US$25) \+ 網域 (.com) | NT$ 1,300 | 一次性必要開支 |
| **雲端與 AI 營運** | Azure App Service \+ Azure SQL \+ Gemini API | NT$ 3,000 \~ 6,000 | 5 個月累計，可申請 Startup Credits 抵扣至 $0 |
| **開發軟體與工具** | Cursor Pro \+ Canva (Ben 使用) | NT$ 2,000 \~ 3,000 | 開發與設計輔助工具 |
| **行銷與封測獎勵** | 14 天封測禮券 \+ Meta/Reels 廣告測試 | NT$ 4,000 \~ 8,000 | 種子用戶獲取與短影音投放測試 |
| **現金預算總計** | **標準推薦方案 (Standard)** | **NT$ 10,300 \~ 18,300** | 精實營運下的總現金需求 |

# **第二部分：系統分析與設計說明書 (SA / SASD)**

## **6\. 系統總體架構 (System Architecture)**

### **6.1 系統部署架構圖 (C/S \+ Web Payment Portal \+ AdMob SSV)**

Plaintext  
\[ 客戶端 1: Android MAUI App \]  
  ├── UI & 狀態管理 (L00 \~ L13 畫面)  
  ├── 裝置端服務 (Camera, ML Kit Vision OCR, Android TTS)  
  ├── AdMob Client SDK (獎勵影音廣告播放)  
  └── 本機儲存 (SecureStorage 存 JWT, SQLite 存快取)  
         │  
         │ (HTTPS / RESTful API / JWT Bearer)  
         ▼  
\[ 伺服器端: ASP.NET Core Minimal API \] ◄─────────────┐  
  ├── 認證與授權中介軟體 (JWT, Google Auth)           │ (REST API / JWT)  
  ├── 業務邏輯層 (字卡 CRUD, 額度扣減, 快取保護)       │  
  ├── 廣告獎勵模組 (AdMob SSV Webhook 驗證)           │  
  ├── 支付與訂閱模組 (TapPay API, 定期扣款 Worker)     │  
  └── AI 代理層 (Word Analysis Agent)                 │  
         │                                           │  
         ├──► \[ Azure SQL Database \]                 │  
         ├──► \[ Google AI Studio (Gemini) \]          │  
         └──► \[ Google AdMob Server (SSV 簽章驗證) \]  │  
                                                     │  
\[ 客戶端 2: Web 結帳加值站 (pay.soraeru.com) \] ───────┘  
  ├── HTML5 / Tailwind CSS \+ TapPay JS SDK  
  └── Google Pay Direct Pay 支付彈窗

### **6.2 技術棧定案 (Tech Stack)**

* **前端 Client：** Visual Studio 2026 \+ **.NET MAUI (Android)**  
* **後端 API：** C\# **ASP.NET Core 8/9 Minimal API**  
* **資料庫：** **Azure SQL Database** (或 Supabase PostgreSQL)  
* **廣告變現：** Google AdMob Rewarded Ads \+ 後端 ECDSA SSV 簽章驗證  
* **Web 加值站：** HTML5 / Tailwind CSS / TapPay JS SDK (Google Pay Direct Pay)  
* **AI 服務：** Google AI Studio (Gemini 1.5 Flash API)  
* **端側 SDK：** Google ML Kit Vision (OCR), Android Native TextToSpeech (TTS)

## **7\. 軟體需求分析 (Requirements Specification)**

### **7.1 功能需求矩陣 (Functional Requirements)**

| 編號 | 功能區塊 | 需求說明 | 負責端 |
| :---- | :---- | :---- | :---- |
| **FR-01** | 身分認證 | 支援 Email/密碼註冊登入、Google OAuth idToken 驗證、核發 JWT | API / App |
| **FR-02** | 端側 OCR | 呼叫相機/相簿選圖，於本機端執行 ML Kit OCR 辨識，原圖不上傳 | App (MAUI) |
| **FR-03** | 多語 AI 分析 | 接收文字，AI 自動偵測來源語言、繁中詞義、讀音與 2\~3 個空耳候選 | API / LLM |
| **FR-04** | 原生 TTS | 依據 API 回傳之 sourceLanguage 呼叫 Android 原生引擎播放語音 | App (MAUI) |
| **FR-05** | 單字卡管理 | 儲存選定候選至雲端，支援列表分頁、多語 Chips 篩選、搜尋與刪除 | API / App |
| **FR-06** | 混合額度管控 | 每日免費 5 次，觀看獎勵廣告每次 \+2 (上限3次/日)；Pro 會員無限次 | API |
| **FR-07** | 廣告 SSV 驗證 | AdMob 伺服器回呼 Webhook，後端驗證 Google ECDSA 簽章後發放額度 | API |
| **FR-08** | 網頁 Google Pay | Web 站點呼叫 TapPay SDK 進行 Google Pay 授權，後端扣款成功升級 VIP | Web / API |
| **FR-09** | 自動續訂排程 | 背景 Worker 每日掃描到期 Pro 用戶，以保存之 CardToken 自動請款 | API Worker |

## **8\. 資料庫設計 (Database Schema)**

採用關聯式資料庫設計（Azure SQL / PostgreSQL）：

### **8.1 Users (使用者表)**

| 欄位名稱 | 型別 | 屬性 | 說明 |
| :---- | :---- | :---- | :---- |
| Id | UNIQUEIDENTIFIER | PK, Default NewID() | 使用者 GUID |
| Email | NVARCHAR(255) | Unique, Not Null | 登入信箱 |
| PasswordHash | NVARCHAR(255) | Nullable | 密碼雜湊 (Google登入為Null) |
| GoogleId | NVARCHAR(100) | Unique, Nullable | Google OAuth UID |
| DisplayName | NVARCHAR(100) | Not Null | 顯示名稱 |
| PlanTier | NVARCHAR(20) | Default 'Free' | 'Free' 或 'Pro' |
| DailyFreeQuota | INT | Default 5 | 今日剩餘免費額度 |
| AdBonusQuota | INT | Default 0 | 今日透過廣告獲得的額度 |
| AdsWatchedToday | INT | Default 0 | 今日已看廣告次數 (上限 3 次) |
| LastQuotaResetDate | DATE | Not Null | 額度重置日期 (UTC) |
| SubscriptionExpiresAt | DATETIME | Nullable | VIP 到期時間 |
| CreatedAt | DATETIME | Default UTC | 帳號建立時間 |

### **8.2 WordCards (單字卡表)**

| 欄位名稱 | 型別 | 屬性 | 說明 |
| :---- | :---- | :---- | :---- |
| Id | UNIQUEIDENTIFIER | PK | 單字卡 GUID |
| UserId | UNIQUEIDENTIFIER | FK \-\> Users(Id) | 歸屬使用者 |
| SourceText | NVARCHAR(100) | Not Null | 原始外語單字/短語 |
| NormalizedText | NVARCHAR(100) | Not Null | 轉小寫/去空白 (查重用) |
| SourceLanguage | NVARCHAR(10) | Not Null | BCP-47 語言碼 (例: ja, ko) |
| LanguageName | NVARCHAR(50) | Not Null | 顯示語言名 (例: 日語) |
| Meaning | NVARCHAR(255) | Not Null | 繁體中文詞義 |
| ReadingText | NVARCHAR(255) | Not Null | 正式讀音文字 (如羅馬音) |
| SelectedMnemonic | NVARCHAR(255) | Not Null | 使用者選定之空耳諧音 |
| Notice | NVARCHAR(500) | Nullable | AI 品質提示或警告 |
| CreatedAt | DATETIME | Default UTC | 建立時間 |
| *複合唯一索引 (Unique Index)：* UserId \+ SourceLanguage \+ NormalizedText |  |  |  |

### **8.3 Payments (支付與交易紀錄表)**

| 欄位名稱 | 型別 | 屬性 | 說明 |
| :---- | :---- | :---- | :---- |
| Id | UNIQUEIDENTIFIER | PK | 交易 GUID |
| UserId | UNIQUEIDENTIFIER | FK \-\> Users(Id) | 交易使用者 |
| Provider | NVARCHAR(50) | Not Null | 例: 'GooglePay\_TapPay' |
| TransactionId | NVARCHAR(100) | Not Null | 金流商交易單號 (RecTradeId) |
| CardToken | NVARCHAR(255) | Nullable | 定期定額扣款用之 CardKey |
| Amount | INT | Not Null | 扣款金額 (TWD) |
| Status | NVARCHAR(20) | Not Null | 'Success', 'Failed' |
| PlanId | NVARCHAR(50) | Not Null | 'pro\_monthly' |
| CreatedAt | DATETIME | Default UTC | 交易時間 |

### **8.4 AdRewardLogs (廣告發放稽核表)**

| 欄位名稱 | 型別 | 屬性 | 說明 |
| :---- | :---- | :---- | :---- |
| Id | UNIQUEIDENTIFIER | PK | 稽核紀錄 GUID |
| UserId | UNIQUEIDENTIFIER | FK \-\> Users(Id) | 觀看者使用者 GUID |
| AdNetwork | NVARCHAR(50) | Not Null | 'Google\_AdMob' |
| RewardAmount | INT | Not Null | 贈送額度 (+2) |
| TransactionId | NVARCHAR(100) | Nullable | AdMob SSV 交易序號 |
| CreatedAt | DATETIME | Default UTC | 觀看完成時間 |

## **9\. API 介面規格 (API Specifications)**

Base URL: \[https://api.soraeru.com/api/v1\](https://api.soraeru.com/api/v1)

### **9.1 認證模組 (Auth)**

* POST /auth/register：{ "email": "...", "password": "..." }  
* POST /auth/login：{ "email": "...", "password": "..." } \-\> 回傳 JWT Token  
* POST /auth/google：{ "idToken": "..." } \-\> 驗證後回傳 JWT Token  
* GET /me：(需 Bearer Token) 回傳使用者資料、PlanTier、DailyFreeQuota、AdBonusQuota、AdsWatchedToday

### **9.2 核心 AI 分析 (Core Analysis)**

* POST /word/analyze  
  * **Headers:** Authorization: Bearer \<Token\>  
  * **Request Body:**

JSON  
    {  
      "text": "こんにちは",  
      "languageHint": "auto",  
      "phoneticPreference": "zhuyin"  
    }  
    

* **Response (200 OK):**

JSON  
    {  
      "sourceText": "こんにちは",  
      "normalizedText": "こんにちは",  
      "sourceLanguage": "ja",  
      "languageDisplayName": "日語",  
      "meaning": "你好",  
      "readingText": "konnichiwa",  
      "mnemonics": \[  
        { "id": "1", "text": "空你吉娃" },  
        { "id": "2", "text": "孔尼其瓦" }  
      \],  
      "notice": "日語常見單字，發音品質穩定。",  
      "remainingDailyQuota": 4  
    }  
    

* **Response (429 Too Many Requests):** 額度耗盡，回傳導引資訊（可看廣告或升級 Pro）。

### **9.3 廣告獎勵回呼 (AdMob Server-Side Verification)**

* GET /ads/admob-ssv-callback  
  * **Query Parameters:** custom\_data (UserId), ad\_network, signature, key\_id, transaction\_id  
  * **後端邏輯：** 驗證 ECDSA 簽章，確認每日觀看上限 \< 3 次後，發放 AdBonusQuota \+= 2 並寫入 AdRewardLogs。

### **9.4 網頁金流與加值 (Payment Portal API)**

* POST /payment/google-pay/subscribe  
  * **Request Body:**

JSON  
    {  
      "prime": "tappay\_prime\_token\_xxx",  
      "planId": "pro\_monthly",  
      "amount": 129  
    }  
    

* **後端邏輯：** 呼叫 TapPay pay-by-prime 請款成功後，更新 Users.PlanTier \= 'Pro'，設定到期日，並記錄 CardToken。

## **10\. 核心業務流程圖**

### **10.1 額度消耗與廣告獎勵流程 (AdMob SSV)**

Plaintext  
\[ .NET MAUI App \]            \[ Google AdMob SDK \]         \[ Soraeru API (後端) \]  
       │                              │                             │  
       │ 1\. 額度用盡，點擊「看廣告 \+2 次」 │                             │  
       ├─────────────────────────────►│                             │  
       │                              │ 2\. 播放 15-30s 獎勵影音廣告   │  
       │                              │ 3\. 播放完畢，Google 伺服器發送 │  
       │                              ├─────── SSV 回呼 (Webhook) ──►│  
       │                              │   (帶 custom\_data \= UserId)  │ 4\. 驗證 Google 簽章  
       │                              │                             │ 5\. DB: AdBonusQuota \+= 2  
       │                              │                             │    AdsWatchedToday \+= 1  
       │ 6\. App 收到 onRewarded 事件  │                             │  
       ├────── GET /api/v1/me ────────┼────────────────────────────►│  
       │◄───── 回傳最新 Quota: 2 ──────┴─────────────────────────────┘

### **10.2 網頁 Google Pay 金流與跨端同步流程**

Plaintext  
 \[ Android App (MAUI) \]                   \[ Web 結帳站 (pay.soraeru.com) \]  
         │                                               │  
         │ 1\. 額度用盡，點擊「升級 VIP」                      │  
         ├──────────────── 引導跳轉至網頁 ──────────────►│  
         │                                               │ 2\. 登入帳號並點擊 Google Pay  
         │                                               │ 3\. 呼叫 TapPay SDK 完成授權  
         │                                               │ 4\. 打 API: POST /payment/google-pay/subscribe  
         │                                               │  
                                                         ▼  
                                            \[ ASP.NET Core API 後端 \]  
                                                         │ 5\. 向 TapPay 請款 NT$ 129 成功  
                                                         │ 6\. 更新 DB: Users.PlanTier \= 'Pro'  
                                                         ▼  
 \[ Android App (MAUI) \]                          \[ Azure SQL DB \]  
         │                                               │  
         │ 7\. 重新開啟或背景 Resume                         │  
         ├───────── GET /api/v1/me (帶 JWT) ────────────►│  
         │◄──────── 回傳 PlanTier: 'Pro', Quota: 無限 ────┘  
         │  
    (成功解鎖 VIP 權益！)

## **11\. 資安與隱私防護 (Security & Privacy)**

> 1. **裝置端圖片隱私邊界 (Camera Privacy)：** 相機/相簿圖片 100% 於 Android 裝置端以 ML Kit Vision 辨識成文字，原圖絕對不上傳至伺服器。  
> 2. **機敏憑證安全：** LLM API Key、AdMob 密鑰與 TapPay Partner Key 嚴禁寫入 App 或前端 Web 程式碼中，全數儲存於 Azure App Settings 環境變數。  
> 3. **金流與廣告防刷安全：**  
   * Web 加值站與 API 端點強制啟用 HTTPS TLS 1.3 加密。  
   * TapPay Prime Token 為一次性密文，防止重放攻擊 (Replay Attack)。  
   * AdMob 獎勵發放嚴格採用 ECDSA 伺服器端簽章驗證，杜絕前端竄改與封包偽造。

## **12\. 工作分解結構與開發時程 (WBS & RACI Matrix)**

### **12.1 16 週專案總體里程碑 (2026/08/08 \~ 2026/11/27)**

Plaintext  
W1-W4 (8/08-9/04)   : 專案啟動、品牌 CI 定案、Auth API、DB Schema 建置、UI/UX Layout  
W5-W8 (9/05-10/02)  : AI Analyze API、MAUI 主畫面開發、Android 原生 OCR/TTS 整合  
W9-W12 (10/03-10/30): 額度控管、AdMob SSV 驗證、Web Google Pay 結帳站、TapPay API 串接、Alpha 測試  
W13-W14 (10/31-11/13): Google Play Console 上架審查、12人 × 14天強制封閉測試  
W15-W16 (11/14-11/27): 正式發布上市、短影音行銷火力全開、異業合作推廣

\#\#\# 12.2 團隊職責分工矩陣 (RACI Matrix)

| 工作模組 | Aeshton (CTO / 技術總監) | Ben (CMO / 行銷總監) | 交付成果 |  
| :--- | :---: | :---: | :--- |  
| \*\*品牌識別與 UI 規範\*\* | 諮詢 (C) | \*\*主責 (A/R)\*\* | Logo、Icon、Stitch/Figma 設計規範 |  
| \*\*後端架構與 AI 代理\*\* | \*\*主責 (A/R)\*\* | 諮詢 (C) | Minimal API、Prompt 調校、快取保護 |  
| \*\*MAUI Android App 開發\*\*| \*\*主責 (A/R)\*\* | 協助 (S) | ML Kit OCR、TTS 發音、畫面刻版 |  
| \*\*AdMob 廣告與 SSV 防刷\*\* | \*\*主責 (A/R)\*\* | \*\*共同主責 (A/R)\*\* | AdMob 單元申請、後端簽章驗證 Webhook |  
| \*\*Web 結帳加值頁與金流\*\* | \*\*主責 (A/R)\*\* | \*\*共同主責 (A/R)\*\* | HTML5/Tailwind 頁面、TapPay Google Pay 串接 |  
| \*\*封閉測試營運 (12人×14天)\*\* | 協助 (S) | \*\*主責 (A/R)\*\* | 勇者測試招募、Bug 收集與修復更新 |  
| \*\*社群短影音與異業合作\*\* | 支援 (C) | \*\*主責 (A/R)\*\* | Reels/Shorts 拍攝、KOL/eSIM 合作案推進 |

\---

\*本文件由 Soraeru 核心團隊 (Aeshton & Ben) 共同審定，作為專案開發、商業營運與上架審查之唯一權威依據。\*  
