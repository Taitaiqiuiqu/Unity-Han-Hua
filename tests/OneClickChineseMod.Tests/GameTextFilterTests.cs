using OneClickChineseMod.Core;

namespace OneClickChineseMod.Tests;

public class GameTextFilterTests
{
    [Fact]
    public void Filter_WithControllerButtons_ReplacesWithPlaceholders()
    {
        var (filtered, map) = GameTextFilter.Filter("[A] 开始游戏 [B] 取消");

        Assert.DoesNotContain("[A]", filtered);
        Assert.DoesNotContain("[B]", filtered);
        Assert.Contains("{K1}", filtered);
        Assert.Contains("{K2}", filtered);
        Assert.Equal("[A]", map["{K1}"]);
        Assert.Equal("[B]", map["{K2}"]);
    }

    [Fact]
    public void Filter_WithDirectionArrows_ReplacesWithPlaceholders()
    {
        var (filtered, map) = GameTextFilter.Filter("按 ← → 移动");

        Assert.DoesNotContain("←", filtered);
        Assert.DoesNotContain("→", filtered);
        Assert.Contains("{K1}", filtered);
        Assert.Contains("{K2}", filtered);
    }

    [Fact]
    public void Filter_WithFunctionKeys_ReplacesWithPlaceholders()
    {
        var (filtered, map) = GameTextFilter.Filter("按 F1 打开菜单 F12 关闭");

        Assert.DoesNotContain("F1", filtered);
        Assert.DoesNotContain("F12", filtered);
        Assert.Contains("{K1}", filtered);
        Assert.Contains("{K2}", filtered);
    }

    [Fact]
    public void Filter_WithModifierKeys_ReplacesCtrlOnly()
    {
        var (filtered, map) = GameTextFilter.Filter("按 Ctrl+Alt+Delete 结束进程");

        Assert.DoesNotContain("Ctrl", filtered);
        Assert.Contains("{K1}", filtered);
        Assert.Single(map);
        Assert.Equal("Ctrl", map["{K1}"]);
    }

    [Fact]
    public void Filter_WithMixedContent_ReplacesOnlyControlChars()
    {
        var (filtered, map) = GameTextFilter.Filter("按 [A] 开始，然后按 [START] 继续");

        Assert.Contains("开始，然后按", filtered);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Filter_WithNoControlChars_ReturnsOriginalText()
    {
        var (filtered, map) = GameTextFilter.Filter("这是一段普通的中文游戏文本");

        Assert.Equal("这是一段普通的中文游戏文本", filtered);
        Assert.Empty(map);
    }

    [Fact]
    public void Filter_WithEmptyString_ReturnsEmpty()
    {
        var (filtered, map) = GameTextFilter.Filter("");

        Assert.Equal("", filtered);
        Assert.Empty(map);
    }

    [Fact]
    public void Filter_WithWhitespaceOnly_ReturnsOriginal()
    {
        var (filtered, map) = GameTextFilter.Filter("   ");

        Assert.Equal("   ", filtered);
        Assert.Empty(map);
    }

    [Fact]
    public void Restore_WithPlaceholders_RestoresOriginal()
    {
        var (filtered, map) = GameTextFilter.Filter("[A] 确认 [B] 取消");

        Assert.Equal(2, map.Count);
        var restored = GameTextFilter.Restore(filtered, map);

        Assert.Equal("[A] 确认 [B] 取消", restored);
    }

    [Fact]
    public void Restore_WithEmptyMap_ReturnsOriginalText()
    {
        var original = "这是一段没有占位符的文本";
        var result = GameTextFilter.Restore(original, new Dictionary<string, string>());

        Assert.Equal(original, result);
    }

    [Fact]
    public void Restore_WithEmptyText_ReturnsEmpty()
    {
        var result = GameTextFilter.Restore("", new Dictionary<string, string> { ["{K1}"] = "[A]" });

        Assert.Equal("", result);
    }

    [Fact]
    public void ContainsOnlyControlChars_WithPureControlChars_ReturnsTrue()
    {
        var result = GameTextFilter.ContainsOnlyControlChars("[A][B][X][Y]");

        Assert.True(result);
    }

    [Fact]
    public void ContainsOnlyControlChars_WithControlCharsAndSpaces_ReturnsTrue()
    {
        var result = GameTextFilter.ContainsOnlyControlChars("  [A]  [B]  ");

        Assert.True(result);
    }

    [Fact]
    public void ContainsOnlyControlChars_WithMixedContent_ReturnsFalse()
    {
        var result = GameTextFilter.ContainsOnlyControlChars("[A] 你好吗？");

        Assert.False(result);
    }

    [Fact]
    public void ContainsOnlyControlChars_WithPureText_ReturnsFalse()
    {
        var result = GameTextFilter.ContainsOnlyControlChars("你好，世界！");

        Assert.False(result);
    }

    [Fact]
    public void ContainsOnlyControlChars_WithEmptyString_ReturnsFalse()
    {
        Assert.False(GameTextFilter.ContainsOnlyControlChars(""));
    }

    [Fact]
    public void ContainsOnlyControlChars_WithWhitespaceOnly_ReturnsFalse()
    {
        Assert.False(GameTextFilter.ContainsOnlyControlChars("   \n\t  "));
    }

    [Fact]
    public void FilterAndRestore_RoundTrip_PreservesContent()
    {
        var original = "按 [A] 攻击，按 [B] 防御，用 ←↑↓→ 移动，按 F1 查看地图";

        var (filtered, map) = GameTextFilter.Filter(original);
        var restored = GameTextFilter.Restore(filtered, map);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Filter_WithSelectInMiddleOfWord_DoesNotReplace()
    {
        var (filtered, map) = GameTextFilter.Filter("SELECT 开始 MENU 设置");

        Assert.Equal("SELECT 开始 MENU 设置", filtered);
        Assert.Empty(map);
    }

    [Fact]
    public void Filter_ReplacesHomeEndPgUpPgDn()
    {
        var (filtered, map) = GameTextFilter.Filter("Home 开头 End 结尾 PgUp 向上 PgDn 向下");

        Assert.DoesNotContain("Home", filtered);
        Assert.DoesNotContain("End", filtered);
        Assert.DoesNotContain("PgUp", filtered);
        Assert.DoesNotContain("PgDn", filtered);
        Assert.Equal(4, map.Count);
    }
}
