namespace DesktopPet.App.Pet;

public readonly record struct ScreenPoint(double X, double Y)
{
    public double DistanceSquared(ScreenPoint other)
    {
        var horizontal = X - other.X;
        var vertical = Y - other.Y;
        return horizontal * horizontal + vertical * vertical;
    }
}

public enum PointerAction
{
    None,
    BeginDrag,
    ContinueDrag,
    EndDrag,
    ClickCandidate
}

public sealed class PetInteractionController(double horizontalDragThreshold, double verticalDragThreshold)
{
    private bool _pressed;
    private bool _dragging;
    private ScreenPoint _pressPoint;

    public void Press(ScreenPoint point)
    {
        _pressed = true;
        _dragging = false;
        _pressPoint = point;
    }

    public PointerAction Move(ScreenPoint point)
    {
        if (!_pressed)
        {
            return PointerAction.None;
        }

        if (!_dragging
            && (Math.Abs(point.X - _pressPoint.X) >= horizontalDragThreshold
                || Math.Abs(point.Y - _pressPoint.Y) >= verticalDragThreshold))
        {
            _dragging = true;
            return PointerAction.BeginDrag;
        }

        return _dragging ? PointerAction.ContinueDrag : PointerAction.None;
    }

    public PointerAction Release()
    {
        if (!_pressed)
        {
            return PointerAction.None;
        }

        _pressed = false;
        if (_dragging)
        {
            _dragging = false;
            return PointerAction.EndDrag;
        }

        return PointerAction.ClickCandidate;
    }

    public void Cancel()
    {
        _pressed = false;
        _dragging = false;
    }
}
