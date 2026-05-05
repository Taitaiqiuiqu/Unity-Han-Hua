namespace OneClickChineseMod.Models;

public enum DeployStatus
{
    Success,
    AlreadyInstalled,
    Failed,
    Cancelled
}

public class DeployResult
{
    public DeployStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> InstalledFiles { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
