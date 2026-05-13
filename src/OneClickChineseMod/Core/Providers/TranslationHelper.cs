using System.Text.Json;
using System.Text.RegularExpressions;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core.Providers;

public static class TranslationHelper
{
    private static readonly Regex JsonBlockRegex = new(
        @"```(?:json)?\s*(\[[\s\S]*?\])\s*```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["ja"] = "日语", ["zh"] = "简体中文", ["zh-Hant"] = "繁体中文",
        ["en"] = "英语", ["ko"] = "韩语", ["fr"] = "法语", ["de"] = "德语", ["es"] = "西班牙语"
    };

    public static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";

    public static string BuildSystemPrompt(ProviderTranslationRequest request)
    {
        var srcName = LanguageNames.GetValueOrDefault(request.SourceLanguage, request.SourceLanguage);
        var tgtName = LanguageNames.GetValueOrDefault(request.TargetLanguage, request.TargetLanguage);

        return $"你是一位专业游戏本地化翻译专家，负责将{srcName}翻译为{tgtName}。重要警告：绝对禁止原样保留任何{srcName}文本不翻译，哪怕看起来像专有名词或术语。必须全部翻译为{tgtName}。规则：\n" +
               "1. 仅输出翻译结果，不要输出任何解释、注释、原文或额外文字；\n" +
               $"2. 禁止保留{srcName}原文——所有{srcName}内容必须翻译为{tgtName}；\n" +
               "3. 保留所有特殊字符、换行、标点符号和格式；\n" +
               $"4. 若无法翻译（如纯符号），只返回单个符号如\"-\"，不要原样返回任何{srcName}文本。\n" +
                "\n" +
                "【输出格式示例】\n" +
                "输入: <text>Load Game</text>\n" +
                "正确: 加载游戏\n" +
                "错误: <text>加载游戏</text> | \"加载游戏\" | ```加载游戏```\n" +
                "关键: <text>仅标记输入边界，你的输出必须是纯翻译文本，不含任何标签、引号或格式标记。";
    }

    public static string BuildBatchSystemPrompt(string sourceLanguage, string targetLanguage)
    {
        var srcName = LanguageNames.GetValueOrDefault(sourceLanguage, sourceLanguage);
        var tgtName = LanguageNames.GetValueOrDefault(targetLanguage, targetLanguage);

        return $"你是一位专业游戏本地化翻译专家，负责将{srcName}翻译为{tgtName}。【核心规则】禁止原样保留任何{srcName}文本——必须全部翻译为{tgtName}，绝对不允许出现未翻译的{srcName}词汇。违反此规则将导致翻译失败。严格遵守：\n" +
            "1. 仅返回纯净JSON数组，禁止输出任何解释、注释、多余文字、表情及markdown代码块；\n" +
            "2. 每条数据必须完整保留原有所有字段，id字段原值完全不变、不得修改、不得缺失；\n" +
            "3. 仅翻译text字段的文本内容，不修改、不处理其他任何字段；\n" +
            "4. 输出条目数量、顺序必须和输入完全一致，不得合并、拆分、调换顺序；\n" +
            $"5. 【关键】禁止保留{srcName}原文——所有{srcName}内容必须翻译为{tgtName}，即使是角色名、装备名、技能名、术语也必须翻译（无可翻译对应词时使用最接近的{tgtName}词汇）；\n" +
            "6. 所有标点、引号、括号、换行、空格、特殊格式符号完全原样保留，不做格式化调整；\n" +
            "7. 翻译简洁精准，贴合游戏剧情/UI文案语境，不扩写、不润色、不增减语义；\n" +
            $"8. 若text为空或纯符号无{srcName}语义，返回单个符号\"-\"，绝不原样返回{srcName}文本；\n" +
            "9. 输出严格标准JSON格式，不添加多余转义、不破坏结构。\n" +
                "\n" +
                "【输出格式示例】\n" +
                "输入: <batch>[{\"id\":1,\"text\":\"Credits\"},{\"id\":2,\"text\":\"Load Game\"}]</batch>\n" +
                "正确: [{\"id\":1,\"text\":\"制作人员\"},{\"id\":2,\"text\":\"加载游戏\"}]\n" +
                "错误: ```json[...]``` | <batch>[...]</batch> | 任何含解释文字的响应\n" +
                "关键: 输出纯JSON数组，<batch>仅标记输入边界，不是输出格式要求。";
    }

    public static string ParseOpenAIResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
            return StripTextTags(content.Trim());
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteWarning($"解析 OpenAI 格式响应失败: {ex.Message}");
            return "";
        }
    }

    public static ProviderBatchTranslationResult ParseBatchArrayResponse(string responseBody, List<ProviderBatchItem> items)
    {
        var results = new Dictionary<long, string>();
        var fallbackIds = new HashSet<long>();
        var requestedIds = new HashSet<long>(items.Select(i => i.Id));

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var msgContent = choices[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";

                    var arrayContent = ExtractJsonArray(msgContent) ?? msgContent;
                     try
                     {
                         ParseBatchEntries(arrayContent, results);
                     }
                     catch (JsonException ex)
                     {
                         ConsoleUtils.WriteWarning($"解析choices.content内层JSON失败: {ex.Message}. 内容: {Truncate(arrayContent, 160)}");
                     }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                ParseBatchEntriesFromElement(root, results);
            }
        }
        catch (JsonException)
        {
            var extracted = ExtractJsonArray(responseBody);
            if (extracted != null)
            {
                try { ParseBatchEntries(extracted, results); }
                catch (JsonException ex)
                {
                    ConsoleUtils.WriteError($"解析批量翻译响应ExtractJsonArray后仍失败: {ex.Message}");
                }
            }
            else
            {
                ConsoleUtils.WriteError($"解析批量翻译响应失败: 无法识别JSON格式");
            }
        }

        foreach (var item in items)
        {
            if (!results.ContainsKey(item.Id))
            {
                results[item.Id] = item.Text;
                fallbackIds.Add(item.Id);
                DiagnosticLogger.Log($"[BatchParse Warning] ID={item.Id} 未在LLM响应中解析到翻译结果，用原文兜底: {Truncate(item.Text, 50)}");
            }
        }

        if (fallbackIds.Count > 0)
        {
            ConsoleUtils.WriteWarning($"批量翻译结果不完整: 解析到 {results.Count - fallbackIds.Count}/{items.Count} 条翻译, {fallbackIds.Count} 条用原文兜底");
        }

        var parsedIds = new HashSet<long>(results.Keys.Except(fallbackIds));
        var extraIds = parsedIds.Except(requestedIds).ToList();
        if (extraIds.Count > 0)
        {
            DiagnosticLogger.Log($"[BatchParse Warning] LLM返回了额外的未请求ID ({extraIds.Count}个): {string.Join(",", extraIds.Take(10))}");
        }

        var missingIds = requestedIds.Except(parsedIds).ToList();
        if (missingIds.Count > 0)
        {
            DiagnosticLogger.Log($"[BatchParse Warning] LLM遗漏了请求的ID ({missingIds.Count}个): {string.Join(",", missingIds.Take(10))}");
        }

        var allFallback = fallbackIds.Count == items.Count;

        return new ProviderBatchTranslationResult
        {
            Success = !allFallback,
            TranslatedTexts = results,
            FallbackItemIds = fallbackIds,
            TotalRequested = items.Count,
            ProviderName = "",
            ErrorMessage = allFallback ? $"批量翻译全部{items.Count}条均未解析到有效结果，需要降级为单条翻译" : null,
            ShouldRetry = !allFallback,
            ShouldFailover = allFallback
        };
    }

    private static void ParseBatchEntries(string jsonArray, Dictionary<long, string> results)
    {
        using var doc = JsonDocument.Parse(jsonArray);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return;
        ParseBatchEntriesFromElement(doc.RootElement, results);
    }

    private static void ParseBatchEntriesFromElement(JsonElement root, Dictionary<long, string> results)
    {
        foreach (var entry in root.EnumerateArray())
        {
            if (!entry.TryGetProperty("id", out var idProp) || !entry.TryGetProperty("text", out var textProp))
                continue;

            if (!TryExtractId(idProp, out var id)) continue;

            var text = textProp.GetString() ?? "";
            results[id] = StripTextTags(text.Trim());
        }
    }

    private static bool TryExtractId(JsonElement idProp, out long id)
    {
        if (idProp.ValueKind == JsonValueKind.Number)
        {
            id = idProp.GetInt64();
            return true;
        }
        if (idProp.ValueKind == JsonValueKind.String && long.TryParse(idProp.GetString(), out var parsedId))
        {
            id = parsedId;
            return true;
        }
        id = 0;
        return false;
    }

    private static string StripTextTags(string text)
    {
        if (text.Length > 13
            && text.StartsWith("<text>", StringComparison.Ordinal)
            && text.EndsWith("</text>", StringComparison.Ordinal))
        {
            return text[6..^7].Trim();
        }
        return text;
    }

    public static string? ExtractJsonArray(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;

        content = content.Trim();

        var jsonBlockMatch = JsonBlockRegex.Match(content);
        if (jsonBlockMatch.Success)
            return jsonBlockMatch.Groups[1].Value.Trim();

        var firstBracket = content.IndexOf('[');
        var lastBracket = content.LastIndexOf(']');

        if (firstBracket >= 0 && lastBracket > firstBracket)
            return content.Substring(firstBracket, lastBracket - firstBracket + 1).Trim();

        return null;
    }
}
