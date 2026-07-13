using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.App.Native;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x00000020;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExNoActivate = 0x08000000;
    internal const int WmMouseActivate = 0x0021;
    internal const int WmDpiChanged = 0x02E0;
    internal const int MaNoActivate = 3;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoSize = 0x0001;
    internal const int RgnOr = 2;
    internal static readonly IntPtr HwndTopmost = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr newLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr window, int index, int newLong);

    internal static void AddExtendedStyles(IntPtr window, int styles)
    {
        if (IntPtr.Size == 8)
        {
            var current = GetWindowLongPtr64(window, GwlExStyle).ToInt64();
            SetWindowLongPtr64(window, GwlExStyle, new IntPtr(current | (uint)styles));
        }
        else
        {
            var current = GetWindowLong32(window, GwlExStyle);
            SetWindowLong32(window, GwlExStyle, current | styles);
        }
    }

    internal static void MoveWindowPhysical(IntPtr window, int x, int y)
    {
        if (!SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, SwpNoActivate | SwpNoZOrder | SwpNoSize))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    internal static IntPtr GetHandle(Window window) => new WindowInteropHelper(window).Handle;

    internal static (double ScaleX, double ScaleY) GetDpiScale(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        return (transform.M11, transform.M22);
    }
}

internal static class AlphaRegionBuilder
{
    public static void Apply(Window window, BitmapSource frame, byte alphaThreshold = 8)
    {
        var handle = NativeMethods.GetHandle(window);
        if (handle == IntPtr.Zero || window.ActualWidth <= 0 || window.ActualHeight <= 0)
        {
            return;
        }

        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var dpi = NativeMethods.GetDpiScale(window);
        var outputWidth = Math.Max(1, (int)Math.Round(window.ActualWidth * dpi.ScaleX));
        var outputHeight = Math.Max(1, (int)Math.Round(window.ActualHeight * dpi.ScaleY));
        var destination = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        if (destination == IntPtr.Zero)
        {
            return;
        }

        var transferred = false;
        try
        {
            for (var outputY = 0; outputY < outputHeight; outputY++)
            {
                var sourceY = Math.Min(converted.PixelHeight - 1, outputY * converted.PixelHeight / outputHeight);
                var runStart = -1;
                for (var outputX = 0; outputX <= outputWidth; outputX++)
                {
                    var visible = false;
                    if (outputX < outputWidth)
                    {
                        var sourceX = Math.Min(converted.PixelWidth - 1, outputX * converted.PixelWidth / outputWidth);
                        visible = pixels[sourceY * stride + sourceX * 4 + 3] >= alphaThreshold;
                    }

                    if (visible && runStart < 0)
                    {
                        runStart = outputX;
                    }
                    else if (!visible && runStart >= 0)
                    {
                        var run = NativeMethods.CreateRectRgn(runStart, outputY, outputX, outputY + 1);
                        if (run != IntPtr.Zero)
                        {
                            NativeMethods.CombineRgn(destination, destination, run, NativeMethods.RgnOr);
                            NativeMethods.DeleteObject(run);
                        }

                        runStart = -1;
                    }
                }
            }

            transferred = NativeMethods.SetWindowRgn(handle, destination, true) != 0;
        }
        finally
        {
            if (!transferred)
            {
                NativeMethods.DeleteObject(destination);
            }
        }
    }
}
