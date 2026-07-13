using DesktopPet.App.Models;

namespace DesktopPet.App.Chat;

public interface IChatCompletionClient
{
    IAsyncEnumerable<ChatDelta> StreamAsync(
        string apiBase,
        string apiKey,
        ChatRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<ApiTestResult> TestAsync(
        string apiBase,
        string apiKey,
        string model,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
