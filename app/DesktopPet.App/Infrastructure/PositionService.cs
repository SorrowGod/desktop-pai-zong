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
        var minimumVisible = Math.Min(32, Math.Max(8, Math.Min(width, height) / 3));
        var proposed = new Rectangle(x, y, width, height);
        var screens = Screen.AllScreens;
        if (screens.Any(screen =>
            {
                var intersection = Rectangle.Intersect(proposed, screen.Bounds);
                return intersection.Width >= minimumVisible && intersection.Height >= minimumVisible;
            }))
        {
            return new Point(x, y);
        }

        var centerX = x + width / 2;
        var centerY = y + height / 2;
        var target = screens
            .OrderBy(screen => DistanceSquaredToRectangle(centerX, centerY, screen.Bounds))
            .FirstOrDefault()
            ?? Screen.PrimaryScreen;
        var bounds = target?.Bounds ?? System.Windows.Forms.SystemInformation.VirtualScreen;
        var left = Math.Clamp(x, bounds.Left - width + minimumVisible, bounds.Right - minimumVisible);
        var top = Math.Clamp(y, bounds.Top - height + minimumVisible, bounds.Bottom - minimumVisible);
        return new Point(left, top);
    }

    private static long DistanceSquaredToRectangle(int x, int y, Rectangle rectangle)
    {
        var closestX = Math.Clamp(x, rectangle.Left, rectangle.Right);
        var closestY = Math.Clamp(y, rectangle.Top, rectangle.Bottom);
        var deltaX = (long)x - closestX;
        var deltaY = (long)y - closestY;
        return deltaX * deltaX + deltaY * deltaY;
    }
}
