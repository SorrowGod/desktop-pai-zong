namespace DesktopPet.App.Pet;

public enum PetAnimationState
{
    Idle,
    Walk,
    Sleep,
    Fall,
    Drag,
    Reaction,
    Happy,
    Sad
}

public sealed class PetStateMachine : IDisposable
{
    private readonly object _sync = new();
    private readonly TimeSpan _sleepDelay;
    private CancellationTokenSource? _scheduledTransition;
    private bool _disposed;

    public PetStateMachine(TimeSpan? sleepDelay = null)
    {
        _sleepDelay = sleepDelay ?? TimeSpan.FromMinutes(8);
    }

    public PetAnimationState State { get; private set; } = PetAnimationState.Idle;

    public event EventHandler<PetAnimationState>? StateChanged;

    public void Start() => Enter(PetAnimationState.Idle);

    public void Enter(PetAnimationState state, TimeSpan? returnToIdleAfter = null)
    {
        CancellationToken token;
        lock (_sync)
        {
            ThrowIfDisposed();
            _scheduledTransition?.Cancel();
            _scheduledTransition?.Dispose();
            _scheduledTransition = new CancellationTokenSource();
            token = _scheduledTransition.Token;
            SetStateLocked(state);
        }

        if (returnToIdleAfter is not null)
        {
            _ = ScheduleReturnToIdleAsync(returnToIdleAfter.Value, token);
        }
        else if (state == PetAnimationState.Idle)
        {
            _ = ScheduleSleepAsync(token);
        }
    }

    public void NotifyInteraction()
    {
        Enter(PetAnimationState.Idle);
    }

    private async Task ScheduleReturnToIdleAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            Enter(PetAnimationState.Idle);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ScheduleSleepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_sleepDelay, cancellationToken);
            lock (_sync)
            {
                if (!_disposed && !cancellationToken.IsCancellationRequested && State == PetAnimationState.Idle)
                {
                    SetStateLocked(PetAnimationState.Sleep);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SetStateLocked(PetAnimationState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            _scheduledTransition?.Cancel();
            _scheduledTransition?.Dispose();
            _scheduledTransition = null;
        }
    }
}
