using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DesktopPet.App.Models;

namespace DesktopPet.App.Chat;

public sealed class OpenAiCompatibleChatClient(HttpClient httpClient) : IChatCompletionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<ChatDelta> StreamAsync(
        string apiBase,
        string apiKey,
        ChatRequest request,
        TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var validation = ApiEndpointNormalizer.Normalize(apiBase, true, apiKey);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error, nameof(apiBase));
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using var message = CreateRequest(validation.Endpoint!, apiKey, request with { Stream = true });
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("AI 请求超时。");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateApiExceptionAsync(response, timeoutSource.Token);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            await foreach (var delta in SseChatParser.ParseAsync(stream, timeoutSource.Token))
            {
                yield return delta;
            }
        }
    }

    public async Task<ApiTestResult> TestAsync(
        string apiBase,
        string apiKey,
        string model,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var validation = ApiEndpointNormalizer.Normalize(apiBase, true, apiKey);
        if (!validation.IsValid)
        {
            var status = validation.Error.Contains("API Key", StringComparison.OrdinalIgnoreCase)
                ? ApiTestStatus.MissingApiKey
                : ApiTestStatus.InvalidAddress;
            return new ApiTestResult(status, validation.Error);
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var request = new ChatRequest(model, [new ChatMessage("user", "请只回复 OK")], false);
        using var message = CreateRequest(validation.Endpoint!, apiKey, request);

        try
        {
            using var response = await httpClient.SendAsync(message, timeoutSource.Token);
            stopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                return await MapErrorAsync(response, stopwatch.Elapsed, timeoutSource.Token);
            }

            var json = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            using var document = JsonDocument.Parse(json);
            var valid = document.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array;
            return valid
                ? new ApiTestResult(ApiTestStatus.Success, "连接成功，模型返回格式有效。", stopwatch.Elapsed)
                : new ApiTestResult(ApiTestStatus.InvalidResponse, "服务返回成功，但内容不是 Chat Completions 格式。", stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiTestResult(ApiTestStatus.Timeout, "连接超时。", stopwatch.Elapsed);
        }
        catch (HttpRequestException exception) when (exception.InnerException is AuthenticationException)
        {
            return new ApiTestResult(ApiTestStatus.TlsError, "TLS 连接失败，请检查证书、系统时间或代理。", stopwatch.Elapsed);
        }
        catch (HttpRequestException)
        {
            return new ApiTestResult(ApiTestStatus.NetworkError, "网络连接失败，请检查地址、DNS、代理和网络。", stopwatch.Elapsed);
        }
        catch (JsonException)
        {
            return new ApiTestResult(ApiTestStatus.InvalidResponse, "服务返回的内容不是有效 JSON。", stopwatch.Elapsed);
        }
    }

    private static HttpRequestMessage CreateRequest(Uri endpoint, string apiKey, ChatRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    private static async Task<HttpRequestException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var result = await MapErrorAsync(response, null, cancellationToken);
        return new HttpRequestException(result.Message, null, response.StatusCode);
    }

    private static async Task<ApiTestResult> MapErrorAsync(
        HttpResponseMessage response,
        TimeSpan? latency,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var normalized = body.Length > 2_000 ? body[..2_000] : body;
        var status = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ApiTestStatus.Unauthorized,
            HttpStatusCode.Forbidden => ApiTestStatus.Forbidden,
            HttpStatusCode.NotFound when normalized.Contains("model", StringComparison.OrdinalIgnoreCase) => ApiTestStatus.ModelNotFound,
            HttpStatusCode.BadRequest when normalized.Contains("model", StringComparison.OrdinalIgnoreCase)
                && (normalized.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("does not exist", StringComparison.OrdinalIgnoreCase)) => ApiTestStatus.ModelNotFound,
            _ => ApiTestStatus.ApiError
        };
        var message = status switch
        {
            ApiTestStatus.Unauthorized => "认证失败（401），请检查 API Key。",
            ApiTestStatus.Forbidden => "访问被拒绝（403），请检查业务空间、模型权限或账户状态。",
            ApiTestStatus.ModelNotFound => "模型不存在或当前业务空间无权使用。",
            _ => $"API 返回错误 {(int)response.StatusCode}。"
        };
        return new ApiTestResult(status, message, latency);
    }
}
