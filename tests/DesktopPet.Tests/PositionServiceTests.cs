using System.Drawing;
using DesktopPet.App.Infrastructure;

namespace DesktopPet.Tests;

[TestClass]
public sealed class PositionServiceTests
{
    private static readonly Rectangle PrimaryWorkArea = new(0, 0, 1536, 912);

    [TestMethod]
    public void PositionFromDisconnectedSecondMonitorMovesFullyIntoPrimaryWorkArea()
    {
        var position = PositionService.EnsureFullyVisible(
            2247,
            1177,
            64,
            64,
            new[] { PrimaryWorkArea });

        Assert.AreEqual(new Point(1472, 848), position);
    }

    [TestMethod]
    public void PositionBehindTaskbarMovesFullyIntoWorkingArea()
    {
        var position = PositionService.EnsureFullyVisible(
            1515,
            939,
            64,
            64,
            new[] { PrimaryWorkArea });

        Assert.AreEqual(new Point(1472, 848), position);
    }

    [TestMethod]
    public void FullyVisiblePositionOnNegativeCoordinateMonitorIsPreserved()
    {
        var workAreas = new[]
        {
            new Rectangle(-1920, 0, 1920, 1040),
            PrimaryWorkArea
        };

        var position = PositionService.EnsureFullyVisible(-100, 200, 64, 64, workAreas);

        Assert.AreEqual(new Point(-100, 200), position);
    }

    [TestMethod]
    public void WindowLargerThanWorkAreaAnchorsAtWorkAreaOrigin()
    {
        var position = PositionService.EnsureFullyVisible(
            300,
            200,
            2000,
            1200,
            new[] { PrimaryWorkArea });

        Assert.AreEqual(Point.Empty, position);
    }
}
