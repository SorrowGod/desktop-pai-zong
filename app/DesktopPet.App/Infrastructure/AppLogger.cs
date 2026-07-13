using System.Text.RegularExpressions;

namespace DesktopPet.App.Infrastructure;

public interface IAppLogger
{
    void Info(string message);
    void Error(string message, Exception? exception = null);
}

public sealed partial class AppLogger(AppPaths paths) : IAppLogger
{
    private readonly object _sync = new();

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        paths.EnsureCreated();
        var safeMessage = Redact(message);
        var safeException = exception is null
            ? string.Empty
            : $" | {exception.GetType().Name}: {Redact(exception.Message)}";
        var line = $"{DateTimeOffset.Now:O} [{level}] {safeMessage}{safeException}{Environment.NewLine}";

        lock (_sync)
        {
            File.AppendAllText(paths.LogFile, line);
        }
    }

    internal static string Redact(string value)
    {
        var redacted = BearerRegex().Replace(value, "Bearer [REDACTED]");
        redacted = ApiKeyRegex().Replace(redacted, "$1[REDACTED]");
        return redacted.Length > 2_000 ? redacted[..2_000] + "…" : redacted;
    }

    [GeneratedRegex("(?i)Bearer\\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerRegex();

    [GeneratedRegex("(?i)(api[_ -]?key\\s*[:=]\\s*)[^\\s,;]+")]
    private static partial Regex ApiKeyRegex();
}
