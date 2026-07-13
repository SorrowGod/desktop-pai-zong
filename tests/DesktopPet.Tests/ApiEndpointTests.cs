using DesktopPet.App.Chat;

namespace DesktopPet.Tests;

[TestClass]
public sealed class ApiEndpointTests
{
    [DataTestMethod]
    [DataRow("https://dashscope.aliyuncs.com/compatible-mode/v1", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions")]
    [DataRow("https://api.deepseek.com/v1/", "https://api.deepseek.com/v1/chat/completions")]
    [DataRow("https://api.moonshot.cn/v1/chat/completions", "https://api.moonshot.cn/v1/chat/completions")]
    [DataRow("http://127.0.0.1:11434/v1", "http://127.0.0.1:11434/v1/chat/completions")]
    public void NormalizesSupportedAddresses(string source, string expected)
    {
        var result = ApiEndpointNormalizer.Normalize(source, true, "test-key");

        Assert.IsTrue(result.IsValid, result.Error);
        Assert.AreEqual(expected, result.Endpoint!.AbsoluteUri.TrimEnd('/'));
    }

    [TestMethod]
    public void RejectsBailianConsoleAddress()
    {
        var result = ApiEndpointNormalizer.Normalize("https://bailian.console.aliyun.com/", true, "test-key");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Error, "控制台");
    }

    [TestMethod]
    public void RejectsRemoteHttp()
    {
        var result = ApiEndpointNormalizer.Normalize("http://example.com/v1", true, "test-key");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Error, "HTTPS");
    }

    [TestMethod]
    public void RejectsRemoteRequestWithoutKey()
    {
        var result = ApiEndpointNormalizer.Normalize("https://example.com/v1", true, string.Empty);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Error, "API Key");
    }

    [TestMethod]
    public void AllowsSavingRemoteAddressWithoutKeyWhenNoRequestIsMade()
    {
        var result = ApiEndpointNormalizer.Normalize("https://example.com/v1", false, string.Empty);

        Assert.IsTrue(result.IsValid, result.Error);
    }
}
