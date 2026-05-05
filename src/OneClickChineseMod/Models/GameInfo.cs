namespace OneClickChineseMod.Models;

public enum GameArchitecture
{
    x86,
    x64,
    Unknown
}

public enum GameType
{
    Mono,
    IL2CPP,
    Unknown
}

public class GameInfo
{
    public string GamePath { get; set; } = string.Empty;
    public string GameRoot { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public GameArchitecture Architecture { get; set; } = GameArchitecture.Unknown;
    public GameType Type { get; set; } = GameType.Unknown;
    public string UnityVersion { get; set; } = string.Empty;
    public bool IsBepInExInstalled { get; set; }
    public string InstalledBepInExVersion { get; set; } = string.Empty;
}
