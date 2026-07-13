using DesktopPet.App.Pet;

namespace DesktopPet.Tests;

[TestClass]
public sealed class PetLogicTests
{
    [TestMethod]
    public void AffectionMeterClampsToValidRange()
    {
        var meter = new AffectionMeter(99);

        Assert.AreEqual(100, meter.Add(8));
        Assert.AreEqual(0, meter.Remove(200));
        Assert.AreEqual(0, new AffectionMeter(-20).Value);
        Assert.AreEqual(100, new AffectionMeter(200).Value);
    }

    [TestMethod]
    public void InteractionOnlyStartsDragAfterSystemThreshold()
    {
        var controller = new PetInteractionController(4, 4);
        controller.Press(new ScreenPoint(100, 100));

        Assert.AreEqual(PointerAction.None, controller.Move(new ScreenPoint(103, 103)));
        Assert.AreEqual(PointerAction.BeginDrag, controller.Move(new ScreenPoint(104, 102)));
        Assert.AreEqual(PointerAction.ContinueDrag, controller.Move(new ScreenPoint(140, 130)));
        Assert.AreEqual(PointerAction.EndDrag, controller.Release());
        Assert.AreEqual(PointerAction.None, controller.Release());
    }

    [TestMethod]
    public void ReleaseWithoutThresholdProducesClickCandidate()
    {
        var controller = new PetInteractionController(4, 4);
        controller.Press(new ScreenPoint(10, 10));
        controller.Move(new ScreenPoint(12, 12));

        Assert.AreEqual(PointerAction.ClickCandidate, controller.Release());
    }

    [TestMethod]
    public async Task DoubleClickCancelsPendingSingleClick()
    {
        using var arbiter = new ClickArbiter(TimeSpan.FromMilliseconds(70), 8);
        var singles = 0;
        var doubles = 0;
        var now = DateTimeOffset.Now;

        var first = arbiter.RegisterAsync(
            new ScreenPoint(100, 100),
            now,
            () => { singles++; return Task.CompletedTask; },
            () => { doubles++; return Task.CompletedTask; });
        await Task.Delay(10);
        var second = arbiter.RegisterAsync(
            new ScreenPoint(104, 102),
            now.AddMilliseconds(30),
            () => { singles++; return Task.CompletedTask; },
            () => { doubles++; return Task.CompletedTask; });

        await Task.WhenAll(first, second);
        await Task.Delay(90);
        Assert.AreEqual(0, singles);
        Assert.AreEqual(1, doubles);
    }

    [TestMethod]
    public async Task SingleClickRunsAfterDoubleClickWindow()
    {
        using var arbiter = new ClickArbiter(TimeSpan.FromMilliseconds(35), 8);
        var singles = 0;

        await arbiter.RegisterAsync(
            new ScreenPoint(100, 100),
            DateTimeOffset.Now,
            () => { singles++; return Task.CompletedTask; },
            () => Task.CompletedTask);

        Assert.AreEqual(1, singles);
    }

    [TestMethod]
    public async Task NewStateCancelsOldTemporaryTransition()
    {
        using var machine = new PetStateMachine(TimeSpan.FromSeconds(5));
        machine.Enter(PetAnimationState.Reaction, TimeSpan.FromMilliseconds(30));
        machine.Enter(PetAnimationState.Drag);

        await Task.Delay(70);
        Assert.AreEqual(PetAnimationState.Drag, machine.State);
    }

    [TestMethod]
    public async Task IdleTransitionsToSleepWithoutMovingStateCompetition()
    {
        using var machine = new PetStateMachine(TimeSpan.FromMilliseconds(25));
        machine.Enter(PetAnimationState.Idle);

        await Task.Delay(70);
        Assert.AreEqual(PetAnimationState.Sleep, machine.State);
    }
}
