using DesktopPet.App.Native;

namespace DesktopPet.Tests;

[TestClass]
public sealed class AlphaRegionGeometryTests
{
    [DataTestMethod]
    [DataRow(64)]
    [DataRow(80)]
    [DataRow(96)]
    public void ScaledAlphaRegionPreservesTransparentCornerAndOpaqueCenter(int outputSize)
    {
        const int sourceSize = 64;
        const int stride = sourceSize * 4;
        var pixels = new byte[stride * sourceSize];
        for (var y = 12; y < 52; y++)
        {
            for (var x = 12; x < 52; x++)
            {
                pixels[y * stride + x * 4 + 3] = 255;
            }
        }

        var runs = AlphaRegionGeometry.BuildRuns(
            pixels,
            sourceSize,
            sourceSize,
            stride,
            outputSize,
            outputSize,
            8);

        Assert.IsFalse(AlphaRegionGeometry.Contains(runs, 0, 0));
        Assert.IsTrue(AlphaRegionGeometry.Contains(runs, outputSize / 2, outputSize / 2));
        Assert.IsFalse(AlphaRegionGeometry.Contains(runs, outputSize - 1, outputSize - 1));
    }
}
