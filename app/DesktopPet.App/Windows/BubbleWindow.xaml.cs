using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopPet.App.Native;
using Screen = System.Windows.Forms.Screen;

namespace DesktopPet.App.Windows;

public partial class BubbleWindow : Window
{
    private readonly DispatcherTimer _hideTimer = new();
    private IntPtr _handle;

    public BubbleWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            NativeMethods.AddExtendedStyles(
                _handle,
                NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExTransparent);
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
    }

    internal void ShowMessage(string message, NativeMethods.Rect petBounds, TimeSpan duration)
    {
        BubbleText.Text = message;
        Show();
        UpdateLayout();
        PlaceNear(petBounds);
        _hideTimer.Stop();
        _hideTimer.Interval = duration;
        _hideTimer.Start();
    }

    public void Stop()
    {
        _hideTimer.Stop();
        Hide();
    }

    private void PlaceNear(NativeMethods.Rect petBounds)
    {
        var screen = Screen.FromRectangle(new System.Drawing.Rectangle(
            petBounds.Left,
            petBounds.Top,
            Math.Max(1, petBounds.Width),
            Math.Max(1, petBounds.Height)));
        var dpi = NativeMethods.GetDpiScale(this);
        var bubbleWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.ScaleX));
        var bubbleHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.ScaleY));
        var work = screen.WorkingArea;
        const int gap = 10;

        var x = petBounds.Right + gap;
        var y = petBounds.Top + (petBounds.Height - bubbleHeight) / 2;
        if (x + bubbleWidth > work.Right)
        {
            x = petBounds.Left - bubbleWidth - gap;
        }

        if (x < work.Left)
        {
            x = Math.Clamp(petBounds.Left, work.Left, Math.Max(work.Left, work.Right - bubbleWidth));
            y = petBounds.Top - bubbleHeight - gap;
            if (y < work.Top)
            {
                y = petBounds.Bottom + gap;
            }
        }

        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - bubbleWidth));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - bubbleHeight));
        NativeMethods.SetWindowPos(
            _handle,
            NativeMethods.HwndTopmost,
            x,
            y,
            bubbleWidth,
            bubbleHeight,
            NativeMethods.SwpNoActivate);
    }
}
