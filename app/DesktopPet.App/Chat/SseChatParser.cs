using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.IO;
using DesktopPet.App.Models;

namespace DesktopPet.App.Chat;

public static class SseChatParser
{
    public static async IAsyncEnumerable<ChatDelta> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        var dataLines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                foreach (var delta in ParseEvent(dataLines))
                {
                    yield return delta;
                }

                yield break;
            }

            if (line.Length == 0)
            {
                var parsed = ParseEvent(dataLines).ToArray();
                dataLines.Clear();
                foreach (var delta in parsed)
                {
                    yield return delta;
                    if (delta.IsDone)
                    {
                        yield break;
                    }
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line[5..].TrimStart());
            }
        }
    }

    private static IEnumerable<ChatDelta> ParseEvent(IReadOnlyList<string> dataLines)
    {
        if (dataLines.Count == 0)
        {
            yield break;
        }

        var data = string.Join("\n", dataLines);
        if (data.Equals("[DONE]", StringComparison.Ordinal))
        {
            yield return new ChatDelta(string.Empty, true);
            yield break;
        }

        using var document = JsonDocument.Parse(data);
        if (document.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("delta", out var delta)
            && delta.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
        {
            yield return new ChatDelta(content.GetString() ?? string.Empty);
        }
    }
}
