namespace Soraeru.ClientLogic.Legal;

/// <summary>
/// In-app privacy policy and AI content disclaimer copy (ticket 10).
/// Stable application content; Play Store hosted URL can point here later (ticket 12).
/// </summary>
public static class LegalDocuments
{
    public const string PrivacyTitle = "隱私權政策";
    public const string AiDisclaimerTitle = "AI 內容聲明";

    public static string PrivacyBody =>
        """
        本政策說明「空耳聯合國」（Soraeru）如何處理您的資料。使用本應用即表示您知悉下列說明。

        一、帳號資料
        我們會處理您用於登入的 Email、顯示名稱，以及 Google 登入時由 Google 提供、經您同意的識別資訊，以便建立與維護帳號、發放登入權杖（JWT），並提供單字本與額度等服務。

        二、學習與分析文字
        您輸入或選取的外語單字／短語會送至伺服器，交由 AI 產生詞義、讀音與近似音候選。請勿提交不必要的個人敏感內容。

        三、圖片與 OCR
        拍照或相簿選圖僅在您的裝置上做文字辨識（OCR）。原圖不上傳至我們的伺服器；送出分析的是您選取後的文字。

        四、本機與雲端單字本
        單字卡可保存在裝置本機；若您使用雲端相關功能，對應的學習資料會依服務設計儲存於伺服器。登出或刪除帳號時，會依產品流程清除本機會話與相關本機資料；刪除帳號會依 API 結果移除雲端帳號與關聯資料。

        五、權限
        應用可能請求網路、相機與相片存取，僅用於登入／分析、拍照與選圖。我們不會將裝置端 OCR 原圖上傳。

        六、聯絡與更新
        本政策可能隨功能調整更新；重大變更時會在應用內或商店頁面提示。若有疑問，請透過應用商店開發者聯絡管道與我們聯繫。
        """;

    public static string AiDisclaimerBody =>
        """
        關於 AI 產生內容與「空耳／近似音」的重要說明：

        • 近似音僅供記憶輔助，請以正式發音為準。應用內提供的系統語音合成（TTS）可用來聆聽正式讀音參考，但不取代母語者或教師指導。
        • AI 可能有誤：詞義、語言判斷、讀音文字與近似音候選皆可能不正確或不完整，請自行查證後再用於學習。
        • 多語品質不一：不同語言的偵測與空耳品質可能有明顯差異；熱門語言通常較穩，小眾語言可能較弱。
        • 空耳／諧音僅為助記技巧，不是標準發音、不是翻譯權威，也不是語言能力證明。
        • 每日 AI 分析次數受帳號額度限制；請合理使用，勿將服務用於違法或侵害他人權益之用途。

        繼續使用本應用，即表示您理解上述限制，並同意以正式發音與可靠來源核對學習內容。
        """;

    public const string PrivacyDocKey = "privacy";
    public const string AiDisclaimerDocKey = "ai";

    public static (string Title, string Body) Resolve(string? docKey)
    {
        if (string.Equals(docKey, AiDisclaimerDocKey, StringComparison.OrdinalIgnoreCase))
            return (AiDisclaimerTitle, AiDisclaimerBody);

        return (PrivacyTitle, PrivacyBody);
    }
}
