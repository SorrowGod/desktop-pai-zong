namespace DesktopPet.App.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? dataRoot = null)
    {
#if DEBUG
        var diagnosticRoot = Environment.GetEnvironmentVariable("DESKTOPPET_DATA_ROOT");
#else
        const string? diagnosticRoot = null;
#endif
        DataRoot = dataRoot
            ?? (!string.IsNullOrWhiteSpace(diagnosticRoot)
                ? diagnosticRoot
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DesktopPet"));
        SettingsFile = Path.Combine(DataRoot, "settings.json");
        ChatHistoryFile = Path.Combine(DataRoot, "chat-history.json");
        LogsDirectory = Path.Combine(DataRoot, "logs");
        LogFile = Path.Combine(LogsDirectory, "app.log");
    }

    public string DataRoot { get; }
    public string SettingsFile { get; }
    public string ChatHistoryFile { get; }
    public string LogsDirectory { get; }
    public string LogFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LogsDirectory);
    }
}
