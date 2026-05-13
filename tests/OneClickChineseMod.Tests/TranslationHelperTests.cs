using OneClickChineseMod.Core.Providers;
using OneClickChineseMod.Models;

namespace OneClickChineseMod.Tests;

public class TranslationHelperTests
{
    [Fact]
    public void BuildSystemPrompt_WithJaToZh_ContainsJapaneseAndChinese()
    {
        var request = new ProviderTranslationRequest
        {
            SourceLanguage = "ja",
            TargetLanguage = "zh"
        };

        var prompt = TranslationHelper.BuildSystemPrompt(request);

        Assert.Contains("日语", prompt);
        Assert.Contains("简体中文", prompt);
        Assert.Contains("禁止", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithEnToZh_ContainsEnglishAndChinese()
    {
        var request = new ProviderTranslationRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "zh-Hant"
        };

        var prompt = TranslationHelper.BuildSystemPrompt(request);

        Assert.Contains("英语", prompt);
        Assert.Contains("繁体中文", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithKoToZh_ContainsKoreanAndChinese()
    {
        var request = new ProviderTranslationRequest
        {
            SourceLanguage = "ko",
            TargetLanguage = "zh"
        };

        var prompt = TranslationHelper.BuildSystemPrompt(request);

        Assert.Contains("韩语", prompt);
    }

    [Fact]
    public void BuildBatchSystemPrompt_WithJaToZh_ContainsCorrectRules()
    {
        var prompt = TranslationHelper.BuildBatchSystemPrompt("ja", "zh");

        Assert.Contains("日语", prompt);
        Assert.Contains("简体中文", prompt);
        Assert.Contains("JSON", prompt);
        Assert.Contains("id", prompt);
        Assert.Contains("text", prompt);
    }

    [Fact]
    public void Truncate_WithShortString_ReturnsOriginal()
    {
        var input = "Hello";

        var result = TranslationHelper.Truncate(input, 10);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Truncate_WithExactLength_ReturnsOriginal()
    {
        var input = "Hello";

        var result = TranslationHelper.Truncate(input, 5);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Truncate_WithLongString_TruncatesAndAppendsDots()
    {
        var input = "Hello, World!";

        var result = TranslationHelper.Truncate(input, 5);

        Assert.Equal("Hello...", result);
    }

    [Fact]
    public void Truncate_WithEmptyString_ReturnsEmpty()
    {
        var result = TranslationHelper.Truncate("", 10);

        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractJsonArray_WithJsonBlock_ReturnsInnerArray()
    {
        var content = "```json\n[{\"id\": 1, \"text\": \"hello\"}]\n```";

        var result = TranslationHelper.ExtractJsonArray(content);

        Assert.Equal("[{\"id\": 1, \"text\": \"hello\"}]", result);
    }

    [Fact]
    public void ExtractJsonArray_WithRawJsonArray_ReturnsArray()
    {
        var content = "[{\"id\": 1, \"text\": \"hello\"}] some trailing text";

        var result = TranslationHelper.ExtractJsonArray(content);

        Assert.Equal("[{\"id\": 1, \"text\": \"hello\"}]", result);
    }

    [Fact]
    public void ExtractJsonArray_WithNoArray_ReturnsNull()
    {
        var content = "This is not a JSON array";

        var result = TranslationHelper.ExtractJsonArray(content);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractJsonArray_WithEmptyString_ReturnsNull()
    {
        Assert.Null(TranslationHelper.ExtractJsonArray(""));
    }

    [Fact]
    public void ExtractJsonArray_WithNestedBrackets_ExtractsFullArray()
    {
        var content = "prefix [{\"id\": 1, \"text\": \"[nested]\"}] suffix";

        var result = TranslationHelper.ExtractJsonArray(content);

        Assert.Equal("[{\"id\": 1, \"text\": \"[nested]\"}]", result);
    }

    [Fact]
    public void ParseOpenAIResponse_WithValidResponse_ExtractsContent()
    {
        var response = """
        {
            "choices": [
                {
                    "message": {
                        "content": "这是翻译结果"
                    }
                }
            ]
        }
        """;

        var result = TranslationHelper.ParseOpenAIResponse(response);

        Assert.Equal("这是翻译结果", result);
    }

    [Fact]
    public void ParseOpenAIResponse_WithInvalidJson_ReturnsEmpty()
    {
        var result = TranslationHelper.ParseOpenAIResponse("not valid json");

        Assert.Equal("", result);
    }

    [Fact]
    public void ParseOpenAIResponse_WithMissingContent_ReturnsEmpty()
    {
        var response = """
        {
            "choices": []
        }
        """;

        var result = TranslationHelper.ParseOpenAIResponse(response);

        Assert.Equal("", result);
    }

    [Fact]
    public void ParseBatchArrayResponse_WithValidBatchResponse_ParsesCorrectly()
    {
        var response = """
        {
            "choices": [
                {
                    "message": {
                        "content": "[{\"id\": 1, \"text\": \"翻译1\"}, {\"id\": 2, \"text\": \"翻译2\"}]"
                    }
                }
            ]
        }
        """;
        var items = new List<ProviderBatchItem>
        {
            new() { Id = 1, Text = "原文1" },
            new() { Id = 2, Text = "原文2" }
        };

        var result = TranslationHelper.ParseBatchArrayResponse(response, items);

        Assert.True(result.Success);
        Assert.Equal(2, result.TranslatedTexts.Count);
        Assert.Equal("翻译1", result.TranslatedTexts[1]);
        Assert.Equal("翻译2", result.TranslatedTexts[2]);
    }

    [Fact]
    public void ParseBatchArrayResponse_WithEmptyResponse_FallsBackAll()
    {
        var response = "{}";
        var items = new List<ProviderBatchItem>
        {
            new() { Id = 1, Text = "原文1" }
        };

        var result = TranslationHelper.ParseBatchArrayResponse(response, items);

        Assert.False(result.Success);
        Assert.True(result.ShouldFailover);
        Assert.Contains(1, result.FallbackItemIds);
    }

    [Fact]
    public void LanguageNames_ContainsExpectedLanguages()
    {
        Assert.Equal("日语", TranslationHelper.LanguageNames["ja"]);
        Assert.Equal("简体中文", TranslationHelper.LanguageNames["zh"]);
        Assert.Equal("繁体中文", TranslationHelper.LanguageNames["zh-Hant"]);
        Assert.Equal("英语", TranslationHelper.LanguageNames["en"]);
        Assert.Equal("韩语", TranslationHelper.LanguageNames["ko"]);
    }
}
