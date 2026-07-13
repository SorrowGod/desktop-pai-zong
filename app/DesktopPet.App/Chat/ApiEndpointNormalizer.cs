namespace DesktopPet.App.Chat;

public sealed record ApiEndpointValidation(bool IsValid, Uri? Endpoint, string Error)
{
    public static ApiEndpointValidation Success(Uri endpoint) => new(true, endpoint, string.Empty);
    public static ApiEndpointValidation Failure(string error) => new(false, null, error);
}

public static class ApiEndpointNormalizer
{
    public static ApiEndpointValidation Normalize(string apiBase, bool requireApiKey, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiBase)
            || !Uri.TryCreate(apiBase.Trim(), UriKind.Absolute, out var uri))
        {
            return ApiEndpointValidation.Failure("API 地址不是有效的绝对 URL。");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query))
        {
            return ApiEndpointValidation.Failure("API 地址不能包含用户信息、查询参数或片段。");
        }

        if (uri.Host.Equals("bailian.console.aliyun.com", StringComparison.OrdinalIgnoreCase))
        {
            return ApiEndpointValidation.Failure("请输入百炼 OpenAI-compatible API 地址，而不是控制台网页地址。");
        }

        var isLocal = uri.IsLoopback
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);

        if (uri.Scheme != Uri.UriSchemeHttps && !(isLocal && uri.Scheme == Uri.UriSchemeHttp))
        {
            return ApiEndpointValidation.Failure("远程 API 必须使用 HTTPS；仅 localhost/127.0.0.1 可使用 HTTP。");
        }

        if (requireApiKey && !isLocal && string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiEndpointValidation.Failure("远程 API 需要 API Key。");
        }

        var builder = new UriBuilder(uri);
        var path = builder.Path.TrimEnd('/');
        if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            path = $"{path}/chat/completions";
        }

        builder.Path = string.IsNullOrEmpty(path) ? "/chat/completions" : path;
        return ApiEndpointValidation.Success(builder.Uri);
    }
}
