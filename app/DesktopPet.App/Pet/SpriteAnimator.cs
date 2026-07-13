using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopPet.App.Pet;

public sealed class SpriteAnimator : IDisposable
{
    private readonly SpriteCatalog _catalog;
    private readonly DispatcherTimer _timer;
    private PetAnimationState _state;
    private int _frameIndex;

    public SpriteAnimator(SpriteCatalog catalog)
    {
        _catalog = catalog;
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += OnTick;
    }

    public event EventHandler<BitmapSource>? FrameChanged;

    public void Start(PetAnimationState initialState)
    {
        SetState(initialState);
        _timer.Start();
    }

    public void SetState(PetAnimationState state)
    {
        _state = state;
        _frameIndex = 0;
        _timer.Interval = _catalog.GetDefinition(state).FrameDuration;
        PublishFrame();
    }

    private void OnTick(object? sender, EventArgs eventArgs)
    {
        var definition = _catalog.GetDefinition(_state);
        _frameIndex = (_frameIndex + 1) % definition.FrameCount;
        PublishFrame();
    }

    private void PublishFrame() => FrameChanged?.Invoke(this, _catalog.GetFrame(_state, _frameIndex));

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
