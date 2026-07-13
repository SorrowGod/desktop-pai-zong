using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.App.Native;
using DesktopPet.App.Pet;

namespace DesktopPet.App.Windows;

public partial class PetWindow : Window
{
    private readonly PetInteractionController _interaction = new(
        SystemParameters.MinimumHorizontalDragDistance,
        SystemParameters.MinimumVerticalDragDistance);
    private readonly ClickArbiter _clickArbiter = new(
        TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime),
        Math.Max(SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance) * 2);
    private IntPtr _handle;
    private NativeMethods.Point _pressCursor;
    private NativeMethods.Rect _pressWindow;
    private BitmapSource? _frame;
    private bool _closing;

    public PetWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => ScheduleRegionUpdate();
        PetImage.MouseLeftButtonDown += OnLeftButtonDown;
        PetImage.MouseMove += OnMouseMove;
        PetImage.MouseLeftButtonUp += OnLeftButtonUp;
        PetImage.LostMouseCapture += (_, _) => _interaction.Cancel();
        BuildContextMenu();
    }

    public event EventHandler? SingleClicked;
    public event EventHandler? DoubleClicked;
    public event EventHandler? FeedRequested;
    public event EventHandler? PetRequested;
    public event EventHandler? PlayRequested;
    public event EventHandler? ChatRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? HideRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? ContextMenuOpened;
    public event EventHandler<(int X, int Y)>? PositionCommitted;

    public void SetPetScale(double scale)
    {
        Width = SpriteCatalog.FrameSize * Math.Clamp(scale, 1.0, 3.0);
        Height = Width;
    }

    public void SetFrame(BitmapSource frame)
    {
        _frame = frame;
        PetImage.Source = frame;
        ScheduleRegionUpdate();
    }

    internal NativeMethods.Rect GetPhysicalBounds()
    {
        if (_handle == IntPtr.Zero || !NativeMethods.GetWindowRect(_handle, out var rectangle))
        {
            return default;
        }

        return rectangle;
    }

    public void MovePhysical(int x, int y)
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.MoveWindowPhysical(_handle, x, y);
        }
    }

    public void ClosePermanently()
    {
        _closing = true;
        _clickArbiter.Dispose();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs eventArgs)
    {
        if (!_closing)
        {
            eventArgs.Cancel = true;
            HideRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(eventArgs);
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        _handle = new WindowInteropHelper(this).Handle;
        NativeMethods.AddExtendedStyles(_handle, NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
        if (HwndSource.FromHwnd(_handle) is { } source)
        {
            source.AddHook(WindowHook);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs) => ApplyRegion();

    private IntPtr WindowHook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmMouseActivate)
        {
            handled = true;
            return new IntPtr(NativeMethods.MaNoActivate);
        }

        if (message == NativeMethods.WmDpiChanged)
        {
            ScheduleRegionUpdate();
        }

        return IntPtr.Zero;
    }

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!NativeMethods.GetCursorPos(out _pressCursor)
            || !NativeMethods.GetWindowRect(_handle, out _pressWindow))
        {
            return;
        }

        _interaction.Press(new ScreenPoint(_pressCursor.X, _pressCursor.Y));
        PetImage.CaptureMouse();
        eventArgs.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (eventArgs.LeftButton != MouseButtonState.Pressed
            || !NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var action = _interaction.Move(new ScreenPoint(cursor.X, cursor.Y));
        if (action is PointerAction.BeginDrag or PointerAction.ContinueDrag)
        {
            NativeMethods.MoveWindowPhysical(
                _handle,
                _pressWindow.Left + cursor.X - _pressCursor.X,
                _pressWindow.Top + cursor.Y - _pressCursor.Y);
            Dragging?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Dragging;

    private async void OnLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        var action = _interaction.Release();
        PetImage.ReleaseMouseCapture();
        eventArgs.Handled = true;

        if (action == PointerAction.EndDrag)
        {
            if (NativeMethods.GetWindowRect(_handle, out var rectangle))
            {
                PositionCommitted?.Invoke(this, (rectangle.Left, rectangle.Top));
            }

            DragEnded?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (action == PointerAction.ClickCandidate && NativeMethods.GetCursorPos(out var cursor))
        {
            await _clickArbiter.RegisterAsync(
                new ScreenPoint(cursor.X, cursor.Y),
                DateTimeOffset.Now,
                () =>
                {
                    SingleClicked?.Invoke(this, EventArgs.Empty);
                    return Task.CompletedTask;
                },
                () =>
                {
                    DoubleClicked?.Invoke(this, EventArgs.Empty);
                    return Task.CompletedTask;
                });
        }
    }

    public event EventHandler? DragEnded;

    private void ApplyRegion()
    {
        if (_frame is not null && IsLoaded)
        {
            AlphaRegionBuilder.Apply(this, _frame);
        }
    }

    private void ScheduleRegionUpdate()
    {
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ApplyRegion);
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => ContextMenuOpened?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(CreateMenuItem("喂食", () => FeedRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("抚摸", () => PetRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("玩耍", () => PlayRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("打开聊天", () => ChatRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("设置", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("隐藏", () => HideRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("退出", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
        ContextMenu = menu;
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
