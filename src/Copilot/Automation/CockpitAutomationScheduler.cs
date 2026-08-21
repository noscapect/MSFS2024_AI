using Msfs2024Ai.Copilot.AircraftAdapters;
using System.Collections.Concurrent;

namespace Msfs2024Ai.Copilot.Automation;

internal sealed class CockpitAutomationScheduler : IDisposable
{
    private readonly AutomationRuntimeGeneration _runtime;
    private readonly ConcurrentQueue<QueuedCockpitCommand> _commands = new();
    private readonly Dictionary<System.Windows.Forms.Timer, GenerationBoundCockpitAction>
        _delayedActions = new();
    private readonly Func<bool> _runtimeAvailable;
    private readonly Func<AircraftVariant?> _currentVariant;
    private readonly Action<string> _log;
    private readonly Action _delayedActionCompleted;

    public CockpitAutomationScheduler(
        Func<bool> runtimeAvailable,
        Func<AircraftVariant?> currentVariant,
        Action<string> log,
        Action delayedActionCompleted,
        long initialGeneration = 0)
    {
        _runtimeAvailable = runtimeAvailable;
        _currentVariant = currentVariant;
        _log = log;
        _delayedActionCompleted = delayedActionCompleted;
        _runtime = new AutomationRuntimeGeneration(initialGeneration);
    }

    public long CurrentGeneration => _runtime.Current;

    public bool HasPendingActions => _runtime.HasPendingActions;

    public bool IsCurrent(long generation) => _runtime.IsCurrent(generation);

    public long AdvanceGeneration() => _runtime.Advance();

    public void Enqueue(string command) =>
        _commands.Enqueue(_runtime.CreateCommand(command));

    public void Drain(Action<string> execute)
    {
        while (_commands.TryDequeue(out var queuedCommand))
        {
            if (!_runtimeAvailable())
            {
                _log(
                    $"Discarded cockpit command because simulator automation is unavailable: {queuedCommand.Command}.");
                continue;
            }
            if (!_runtime.IsCurrent(queuedCommand.Generation))
            {
                _log(
                    $"Discarded stale cockpit command from generation {queuedCommand.Generation}; current generation is {_runtime.Current}.");
                continue;
            }

            execute(queuedCommand.Command);
        }
    }

    public System.Windows.Forms.Timer Schedule(
        int delayMs,
        Action action,
        string label,
        AircraftVariant? expectedVariant = null)
    {
        var timer = new System.Windows.Forms.Timer { Interval = delayMs };
        var guardedAction = Track(timer);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _delayedActions.Remove(timer);
            timer.Dispose();
            var available = _runtimeAvailable()
                            && (!expectedVariant.HasValue
                                || _currentVariant() == expectedVariant.Value);
            if (!guardedAction.TryExecute(available, action))
            {
                _log(
                    $"Discarded stale delayed cockpit action '{label}' from generation {guardedAction.Generation}; current generation is {_runtime.Current}.");
            }
            _delayedActionCompleted();
        };
        timer.Start();
        return timer;
    }

    public GenerationBoundCockpitAction Track(
        System.Windows.Forms.Timer timer)
    {
        var guardedAction = _runtime.CaptureDelayedAction();
        _delayedActions.Add(timer, guardedAction);
        return guardedAction;
    }

    public void Complete(System.Windows.Forms.Timer timer)
    {
        timer.Stop();
        if (_delayedActions.TryGetValue(timer, out var guardedAction))
        {
            _delayedActions.Remove(timer);
            guardedAction.Dispose();
        }
        timer.Dispose();
    }

    public long InvalidateLiveWork()
    {
        var generation = AdvanceGeneration();
        CancelDelayedActions();
        while (_commands.TryDequeue(out _))
        {
        }
        return generation;
    }

    public void CancelDelayedActions()
    {
        foreach (var pair in _delayedActions.ToArray())
        {
            pair.Key.Stop();
            pair.Key.Dispose();
            pair.Value.Dispose();
        }
        _delayedActions.Clear();
    }

    public void Dispose() => CancelDelayedActions();
}
