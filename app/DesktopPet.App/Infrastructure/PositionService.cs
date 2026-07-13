using System.Drawing;
using DesktopPet.App.Models;
using DesktopPet.App.Native;
using Screen = System.Windows.Forms.Screen;

namespace DesktopPet.App.Infrastructure;

internal static class PositionService
{
    public static Point GetInitialPosition(AppSettings settings, NativeMethods.Rect petBounds)
    {
        if (settings.PetLeft is not null && settings.PetTop is not null)
        {
            return EnsurePartiallyVisible(
                (int)Math.Round(settings.PetLeft.Value),
                (int)Math.Round(settings.PetTop.Value),
                Math.Max(1, petBounds.Width),
                Math.Max(1, petBounds.Height));
        }

        var work = Screen.PrimaryScreen?.WorkingArea ?? System.Windows.Forms.SystemInformation.VirtualScreen;
        return new Point(
            work.Right - Math.Max(1, petBounds.Width) - 36,
            work.Bottom - Math.Max(1, petBounds.Height) - 36);
    }

    public static Point EnsurePartiallyVisible(int x, int y, int width, int height)
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        var minimumVisible = Math.Min(32, Math.Max(8, Math.Min(width, height) / 3));
        var left = Math.Clamp(x, virtualScreen.Left - width + minimumVisible, virtualScreen.Right - minimumVisible);
        var top = Math.Clamp(y, virtualScreen.Top - height + minimumVisible, virtualScreen.Bottom - minimumVisible);
        return new Point(left, top);
    }
}
