using System.Drawing;
using DesktopPet.App.Models;
using DesktopPet.App.Native;
using Screen = System.Windows.Forms.Screen;

namespace DesktopPet.App.Infrastructure;

internal static class PositionService
{
    public static Point GetInitialPosition(AppSettings settings, NativeMethods.Rect petBounds)
    {
        var width = Math.Max(1, petBounds.Width);
        var height = Math.Max(1, petBounds.Height);
        var workAreas = GetWorkAreas();

        if (settings.PetLeft is not null && settings.PetTop is not null)
        {
            return EnsureFullyVisible(
                (int)Math.Round(settings.PetLeft.Value),
                (int)Math.Round(settings.PetTop.Value),
                width,
                height,
                workAreas);
        }

        var work = Screen.PrimaryScreen?.WorkingArea ?? workAreas[0];
        return EnsureFullyVisible(
            work.Right - width - 36,
            work.Bottom - height - 36,
            width,
            height,
            workAreas);
    }

    internal static Point EnsureFullyVisible(
        int x,
        int y,
        int width,
        int height,
        IReadOnlyCollection<Rectangle> workAreas)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var usableAreas = workAreas
            .Where(area => area.Width > 0 && area.Height > 0)
            .ToArray();
        if (usableAreas.Length == 0)
        {
            return new Point(x, y);
        }

        var proposed = new Rectangle(x, y, width, height);
        if (usableAreas.Any(area => area.Contains(proposed)))
        {
            return new Point(x, y);
        }

        var centerX = x + width / 2;
        var centerY = y + height / 2;
        var target = usableAreas
            .OrderBy(area => DistanceSquaredToRectangle(centerX, centerY, area))
            .First();
        var left = ClampAxis(x, width, target.Left, target.Right);
        var top = ClampAxis(y, height, target.Top, target.Bottom);
        return new Point(left, top);
    }

    private static Rectangle[] GetWorkAreas()
    {
        var workAreas = Screen.AllScreens
            .Select(screen => screen.WorkingArea)
            .Where(area => area.Width > 0 && area.Height > 0)
            .ToArray();
        if (workAreas.Length > 0)
        {
            return workAreas;
        }

        var fallback = Screen.PrimaryScreen?.WorkingArea
            ?? System.Windows.Forms.SystemInformation.WorkingArea;
        return [fallback];
    }

    private static int ClampAxis(int position, int size, int start, int end)
    {
        var available = end - start;
        if (available <= 0 || size >= available)
        {
            return start;
        }

        return Math.Clamp(position, start, end - size);
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
