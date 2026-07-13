using System.Net;
using DesktopPet.App.Chat;

namespace DesktopPet.Tests;

[TestClass]
public sealed class ChatClientTests
{
    [TestMethod]
    public async Task TestAsyncClassifiesTimeout()
    {
        using var httpClient = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var client = new OpenAiCompatibleChatClient(httpClient);

        var result = await client.TestAsync(
            "https://example.com/v1",
            "test-key",
            "model",
            TimeSpan.FromMilliseconds(35));

        Assert.AreEqual(DesktopPet.App.Models.ApiTestStatus.Timeout, result.Status);
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.Unauthorized, DesktopPet.App.Models.ApiTestStatus.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden, DesktopPet.App.Models.ApiTestStatus.Forbidden)]
    public async Task TestAsyncClassifiesAuthenticationErrors(HttpStatusCode statusCode, DesktopPet.App.Models.ApiTestStatus expected)
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{\"error\":{\"message\":\"denied\"}}")
            })));
        var client = new OpenAiCompatibleChatClient(httpClient);

        var result = await client.TestAsync("https://example.com/v1", "test-key", "model", TimeSpan.FromSeconds(1));

        Assert.AreEqual(expected, result.Status);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
