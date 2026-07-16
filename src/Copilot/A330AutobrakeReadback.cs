namespace Msfs2024Ai.Copilot;

/// <summary>
/// Resolves the A330's three latched autobrake pushbutton Input Events into
/// one active level. The aircraft does not clear an earlier button's Input
/// Event when another level is selected, so the most recent transition is
/// authoritative. On attachment, the lowest asserted level represents the
/// normal MAX-to-LOW operating sequence used by the flows.
/// </summary>
internal sealed class A330AutobrakeReadback
{
    private readonly double?[] _states = new double?[3];
    private double? _resolvedLevel;

    public double? GetState(int index) => _states[index];

    public double? Level => _resolvedLevel;

    public void Update(int index, double value)
    {
        var previous = _states[index];
        _states[index] = value;

        if (previous.HasValue && Math.Abs(previous.Value - value) >= 0.1)
        {
            if (value >= 0.5)
            {
                _resolvedLevel = index + 1;
            }
            else if (_resolvedLevel.HasValue
                     && Math.Abs(_resolvedLevel.Value - (index + 1)) < 0.1)
            {
                _resolvedLevel = 0;
            }
        }

        if (!_resolvedLevel.HasValue && _states.All(state => state.HasValue))
        {
            var firstAsserted = Array.FindIndex(_states, state => state!.Value >= 0.5);
            _resolvedLevel = firstAsserted >= 0 ? firstAsserted + 1 : 0;
        }
    }

    public void Reset()
    {
        Array.Clear(_states, 0, _states.Length);
        _resolvedLevel = null;
    }
}
