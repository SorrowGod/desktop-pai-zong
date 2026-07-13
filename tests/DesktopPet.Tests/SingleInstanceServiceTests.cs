using DesktopPet.App.Infrastructure;

namespace DesktopPet.Tests;

[TestClass]
public sealed class SingleInstanceServiceTests
{
    [TestMethod]
    public async Task SecondaryObjectSignalsPrimaryListener()
    {
        var instanceId = $"DesktopPet.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceService(instanceId);
        Assert.IsTrue(primary.IsFirstInstance);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.StartListening(command =>
        {
            received.TrySetResult(command);
            return Task.CompletedTask;
        });

        using var secondary = new SingleInstanceService(instanceId);
        Assert.IsFalse(secondary.IsFirstInstance);
        await secondary.SignalPrimaryAsync("show");

        Assert.AreEqual("show", await received.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }
}
