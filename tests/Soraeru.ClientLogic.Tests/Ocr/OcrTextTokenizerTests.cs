using Shouldly;
using Soraeru.ClientLogic.Ocr;

namespace Soraeru.ClientLogic.Tests.Ocr;

public sealed class OcrTextTokenizerTests
{
    [Fact]
    public void Tokenize_splits_whitespace_separated_words_preserving_order()
    {
        var tokens = OcrTextTokenizer.Tokenize("hello  world\nไทย");

        tokens.ShouldBe(["hello", "world", "ไทย"]);
    }

    [Fact]
    public void Tokenize_empty_or_whitespace_returns_empty()
    {
        OcrTextTokenizer.Tokenize(null).ShouldBeEmpty();
        OcrTextTokenizer.Tokenize("   \n\t  ").ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_dedupes_exact_duplicates_keeping_first()
    {
        var tokens = OcrTextTokenizer.Tokenize("abc xyz abc");

        tokens.ShouldBe(["abc", "xyz"]);
    }

    [Fact]
    public void Tokenize_truncates_tokens_longer_than_50_chars()
    {
        var longWord = new string('あ', 60);
        var tokens = OcrTextTokenizer.Tokenize(longWord);

        tokens.Count.ShouldBe(1);
        tokens[0].Length.ShouldBe(50);
        tokens[0].ShouldBe(longWord[..50]);
    }

    [Fact]
    public void Tokenize_unspaced_cjk_run_becomes_single_candidate()
    {
        var tokens = OcrTextTokenizer.Tokenize("日本語の単語");

        tokens.ShouldBe(["日本語の単語"]);
    }

    [Fact]
    public void Tokenize_filters_icon_noise_beside_cyrillic()
    {
        var tokens = OcrTextTokenizer.Tokenize("$ ) Я женщина. PP\" Ч");

        tokens.ShouldBe(["Я", "женщина."]);
    }

    [Fact]
    public void StripNoiseTokens_removes_junk_keeps_cyrillic_words()
    {
        var cleaned = OcrTextTokenizer.StripNoiseTokens("$ ) Я женщина.\nPP\" Ч");

        cleaned.ShouldBe("Я женщина.");
    }

    [Fact]
    public void Tokenize_keeps_latin_when_no_cyrillic_context()
    {
        var tokens = OcrTextTokenizer.Tokenize("hello PP world");

        tokens.ShouldBe(["hello", "PP", "world"]);
    }

    [Fact]
    public void IsLikelyVocabularyToken_rejects_symbol_only()
    {
        OcrTextTokenizer.IsLikelyVocabularyToken("$)", cyrillicContext: true).ShouldBeFalse();
        OcrTextTokenizer.IsLikelyVocabularyToken("PP\"", cyrillicContext: true).ShouldBeFalse();
    }

    [Fact]
    public void Tokenize_duolingo_bubble_strips_speaker_icon_debris()
    {
        var tokens = OcrTextTokenizer.Tokenize("он Это Фоны pea не город.");

        tokens.ShouldBe(["Это", "не", "город."]);
    }

    [Fact]
    public void StripNoiseTokens_duolingo_bubble_keeps_sentence()
    {
        var cleaned = OcrTextTokenizer.StripNoiseTokens("он Это Фоны pea не город.");

        cleaned.ShouldBe("Это не город.");
    }

    [Fact]
    public void Tokenize_three_button_row_unchanged()
    {
        var tokens = OcrTextTokenizer.Tokenize("Девочка токо яблоко");

        tokens.ShouldBe(["Девочка", "токо", "яблоко"]);
    }

    [Fact]
    public void Tokenize_keeps_short_cyrillic_sentence_starter_at_beginning()
    {
        var tokens = OcrTextTokenizer.Tokenize("Он идёт");

        tokens.ShouldBe(["Он", "идёт"]);
    }

    [Fact]
    public void Tokenize_drops_latin_crumbs_in_cyrillic_context()
    {
        OcrTextTokenizer.IsLikelyVocabularyToken("pea", cyrillicContext: true).ShouldBeFalse();
        OcrTextTokenizer.IsLikelyVocabularyToken("OK", cyrillicContext: true).ShouldBeFalse();
        OcrTextTokenizer.IsLikelyVocabularyToken("WiFi", cyrillicContext: true).ShouldBeTrue();
    }

    [Fact]
    public void ScoreCyrillicLineNoise_prefers_cleaner_line()
    {
        var noisy = OcrTextTokenizer.ScoreCyrillicLineNoise("он Это Фоны pea не город.");
        var clean = OcrTextTokenizer.ScoreCyrillicLineNoise("Это не город.");

        noisy.ShouldBeGreaterThan(clean);
    }
}
