using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.App.Pet;

public sealed record AnimationDefinition(int Row, int StartColumn, int FrameCount, TimeSpan FrameDuration);

public sealed class SpriteCatalog
{
    public const int FrameSize = 64;
    private readonly BitmapSource _sheet;
    private readonly IReadOnlyDictionary<PetAnimationState, AnimationDefinition> _definitions =
        new SpriteCatalogDefinitionSource().Definitions;

    public SpriteCatalog()
    {
        var resource = System.Windows.Application.GetResourceStream(
                new Uri("/DesktopPet;component/Assets/patrick-sprites.png", UriKind.Relative))
            ?? throw new InvalidOperationException("找不到嵌入的派大星精灵图。");
        using var stream = resource.Stream;
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        _sheet = decoder.Frames[0];
        _sheet.Freeze();
    }

    public AnimationDefinition GetDefinition(PetAnimationState state) => _definitions[state];

    internal static AnimationDefinition GetDefinitionForTesting(PetAnimationState state) =>
        new SpriteCatalogDefinitionSource().Definitions[state];

    public BitmapSource GetFrame(PetAnimationState state, int frameIndex)
    {
        var definition = GetDefinition(state);
        var index = Math.Clamp(frameIndex, 0, definition.FrameCount - 1);
        var frame = new CroppedBitmap(
            _sheet,
            new Int32Rect((definition.StartColumn + index) * FrameSize, definition.Row * FrameSize, FrameSize, FrameSize));
        frame.Freeze();
        return frame;
    }
}

internal sealed class SpriteCatalogDefinitionSource
{
    public IReadOnlyDictionary<PetAnimationState, AnimationDefinition> Definitions { get; } =
        new Dictionary<PetAnimationState, AnimationDefinition>
        {
            [PetAnimationState.Idle] = new(0, 0, 4, TimeSpan.FromMilliseconds(260)),
            [PetAnimationState.Walk] = new(1, 0, 6, TimeSpan.FromMilliseconds(130)),
            [PetAnimationState.Sleep] = new(2, 0, 4, TimeSpan.FromMilliseconds(500)),
            [PetAnimationState.Fall] = new(3, 0, 2, TimeSpan.FromMilliseconds(160)),
            [PetAnimationState.Drag] = new(3, 2, 1, TimeSpan.FromMilliseconds(250)),
            [PetAnimationState.Reaction] = new(3, 3, 4, TimeSpan.FromMilliseconds(120)),
            [PetAnimationState.Happy] = new(4, 0, 4, TimeSpan.FromMilliseconds(130)),
            [PetAnimationState.Sad] = new(5, 0, 3, TimeSpan.FromMilliseconds(320))
        };
}
