namespace TheUnseenHand.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 4;
    public TargetSettings Target { get; set; } = new();
    public MacroSettings Macro { get; set; } = new();
}

public sealed class TargetSettings
{
    public string ProcessName { get; set; } = "notepad.exe";
}

public sealed class MacroSettings
{
    public List<MacroAction> Actions { get; set; } = new();
}
