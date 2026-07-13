using System.Text.Json.Serialization;

namespace DesktopPet.App.Models;

public sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record ChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream = true);

public sealed record ChatDelta(string Content, bool IsDone = false);

public enum ApiTestStatus
{
    Success,
    InvalidAddress,
    MissingApiKey,
    Unauthorized,
    Forbidden,
    ModelNotFound,
    TlsError,
    NetworkError,
    Timeout,
    InvalidResponse,
    ApiError
}

public sealed record ApiTestResult(ApiTestStatus Status, string Message, TimeSpan? Latency = null)
{
    public bool IsSuccess => Status == ApiTestStatus.Success;
}
