namespace Msfs2024Ai.Copilot.SayIntentions;

internal readonly struct SayIntentionsCommunicationChange
{
    public SayIntentionsCommunicationChange(bool incomingChanged, bool outgoingChanged)
    {
        IncomingChanged = incomingChanged;
        OutgoingChanged = outgoingChanged;
    }

    public bool IncomingChanged { get; }
    public bool OutgoingChanged { get; }
    public bool HasChanges => IncomingChanged || OutgoingChanged;
}

internal sealed class SayIntentionsCommunicationTracker
{
    private readonly Dictionary<long, Snapshot> _snapshots = new();

    public void Reset() => _snapshots.Clear();

    public void Prime(IEnumerable<SayIntentionsCommunication> communications)
    {
        foreach (var communication in communications)
        {
            _snapshots[communication.Id] = Snapshot.From(communication);
        }
    }

    public SayIntentionsCommunicationChange Observe(
        SayIntentionsCommunication communication)
    {
        _snapshots.TryGetValue(communication.Id, out var previous);
        var incomingChanged = !string.IsNullOrWhiteSpace(communication.IncomingMessage)
                              && !string.Equals(
                                  previous?.Incoming,
                                  communication.IncomingMessage,
                                  StringComparison.Ordinal);
        var outgoingChanged = !string.IsNullOrWhiteSpace(communication.OutgoingMessage)
                              && !string.Equals(
                                  previous?.Outgoing,
                                  communication.OutgoingMessage,
                                  StringComparison.Ordinal);
        _snapshots[communication.Id] = Snapshot.From(communication);
        return new SayIntentionsCommunicationChange(
            incomingChanged,
            outgoingChanged);
    }

    private sealed class Snapshot
    {
        public string Incoming { get; private set; } = "";
        public string Outgoing { get; private set; } = "";

        public static Snapshot From(SayIntentionsCommunication communication) =>
            new()
            {
                Incoming = communication.IncomingMessage,
                Outgoing = communication.OutgoingMessage
            };
    }
}
