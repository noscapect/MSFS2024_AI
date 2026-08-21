using System.Threading;

namespace Msfs2024Ai.Copilot.Automation;

internal sealed class AutomationRuntimeGeneration
{
    private long _current;
    private int _pendingActions;

    public AutomationRuntimeGeneration(long initialGeneration = 0)
    {
        _current = initialGeneration;
    }

    public long Current => Interlocked.Read(ref _current);

    public bool HasPendingActions => Volatile.Read(ref _pendingActions) > 0;

    public long Advance() => Interlocked.Increment(ref _current);

    public bool IsCurrent(long generation) => generation == Current;

    public QueuedCockpitCommand CreateCommand(string command) =>
        new(command, Current, DateTime.UtcNow);

    public GenerationBoundCockpitAction CaptureDelayedAction()
    {
        var generation = Current;
        Interlocked.Increment(ref _pendingActions);
        return new GenerationBoundCockpitAction(this, generation);
    }

    internal void CompleteDelayedAction() => Interlocked.Decrement(ref _pendingActions);
}
