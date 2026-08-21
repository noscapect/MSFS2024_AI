using System.Threading;

namespace Msfs2024Ai.Copilot.Automation;

internal sealed class GenerationBoundCockpitAction : IDisposable
{
    private readonly AutomationRuntimeGeneration _runtime;
    private int _completed;

    internal GenerationBoundCockpitAction(
        AutomationRuntimeGeneration runtime,
        long generation)
    {
        _runtime = runtime;
        Generation = generation;
    }

    public long Generation { get; }

    public bool IsCurrent => Volatile.Read(ref _completed) == 0
                             && _runtime.IsCurrent(Generation);

    public bool TryExecute(bool runtimeAvailable, Action action)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return false;
        }

        try
        {
            if (!runtimeAvailable || !_runtime.IsCurrent(Generation))
            {
                return false;
            }

            action();
            return true;
        }
        finally
        {
            _runtime.CompleteDelayedAction();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            _runtime.CompleteDelayedAction();
        }
    }
}
