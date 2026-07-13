namespace DesktopPet.App.Pet;

public sealed class ClickArbiter(TimeSpan doubleClickTime, double doubleClickDistance) : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _pendingClick;
    private ScreenPoint _firstPoint;
    private DateTimeOffset _firstTime;
    private bool _disposed;

    public Task RegisterAsync(
        ScreenPoint point,
        DateTimeOffset timestamp,
        Func<Task> singleClick,
        Func<Task> doubleClick)
    {
        CancellationToken token;
        var isDoubleClick = false;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pendingClick is not null
                && timestamp - _firstTime <= doubleClickTime
                && point.DistanceSquared(_firstPoint) <= doubleClickDistance * doubleClickDistance)
            {
                _pendingClick.Cancel();
                _pendingClick.Dispose();
                _pendingClick = null;
                isDoubleClick = true;
                token = CancellationToken.None;
            }
            else
            {
                _pendingClick?.Cancel();
                _pendingClick?.Dispose();
                _pendingClick = new CancellationTokenSource();
                token = _pendingClick.Token;
                _firstPoint = point;
                _firstTime = timestamp;
            }
        }

        return isDoubleClick ? doubleClick() : CompleteSingleClickAsync(singleClick, token);
    }

    private async Task CompleteSingleClickAsync(Func<Task> singleClick, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(doubleClickTime, cancellationToken);
            await singleClick();
            lock (_sync)
            {
                if (_pendingClick?.Token == cancellationToken)
                {
                    _pendingClick.Dispose();
                    _pendingClick = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingClick?.Cancel();
            _pendingClick?.Dispose();
            _pendingClick = null;
        }
    }
}
