using System.Text.Json;
using DesktopPet.App.Infrastructure;
using DesktopPet.App.Models;
using DesktopPet.App.Security;
using DesktopPet.App.Services;

namespace DesktopPet.Tests;

[TestClass]
public sealed class PersistenceTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "DesktopPet.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [TestMethod]
    public void DpapiRoundTripDoesNotContainPlaintext()
    {
        var protector = new DpapiSecretProtector();
        const string plaintext = "unit-test-secret-value";

        var encrypted = protector.Protect(plaintext);

        Assert.AreNotEqual(plaintext, encrypted);
        Assert.IsFalse(encrypted.Contains(plaintext, StringComparison.Ordinal));
        Assert.AreEqual(plaintext, protector.Unprotect(encrypted));
    }

    [TestMethod]
    public async Task SettingsStorePersistsOnlyProtectedKey()
    {
        var paths = new AppPaths(_directory);
        var service = new SettingsService(paths, new DpapiSecretProtector());
        var settings = new AppSettings();
        const string plaintext = "settings-test-secret";
        service.SetApiKey(settings, plaintext);

        await service.SaveAsync(settings);
        var json = await File.ReadAllTextAsync(paths.SettingsFile);

        Assert.IsFalse(json.Contains(plaintext, StringComparison.Ordinal));
        Assert.AreEqual(plaintext, service.GetApiKey(await service.LoadAsync()));
    }

    [TestMethod]
    public async Task AtomicStoreLeavesNoTemporaryFiles()
    {
        var path = Path.Combine(_directory, "value.json");
        var store = new AtomicJsonStore<SampleValue>(path);

        await store.SaveAsync(new SampleValue("first"));
        await store.SaveAsync(new SampleValue("second"));
        var loaded = await store.LoadOrDefaultAsync(() => new SampleValue("default"));

        Assert.AreEqual("second", loaded.Value);
        Assert.AreEqual(0, Directory.GetFiles(_directory, "*.tmp").Length);
    }

    [TestMethod]
    public async Task CorruptJsonIsBackedUpAndDefaultsAreReturned()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{ definitely-not-json");
        var store = new AtomicJsonStore<SampleValue>(path);

        var loaded = await store.LoadOrDefaultAsync(() => new SampleValue("default"));

        Assert.AreEqual("default", loaded.Value);
        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(1, Directory.GetFiles(_directory, "settings.json.corrupt-*").Length);
    }

    [TestMethod]
    public async Task ChatHistoryKeepsOnlyLatestTwentyUserAssistantMessages()
    {
        var paths = new AppPaths(_directory);
        var service = new ChatHistoryService(paths);
        var messages = Enumerable.Range(1, 30)
            .Select(index => new ChatMessage(index % 2 == 0 ? "assistant" : "user", $"message-{index}"))
            .Prepend(new ChatMessage("system", "do not persist"));

        await service.SaveAsync(messages);
        var loaded = await service.LoadAsync();

        Assert.AreEqual(20, loaded.Count);
        Assert.AreEqual("message-11", loaded[0].Content);
        Assert.AreEqual("message-30", loaded[^1].Content);
        Assert.IsFalse(loaded.Any(message => message.Role == "system"));
    }

    private sealed record SampleValue(string Value);
}
