namespace Msfs2024Ai.Copilot.Automation;

internal sealed class QueuedCockpitCommand
{
    public QueuedCockpitCommand(string command, long generation, DateTime createdUtc)
    {
        Command = command;
        Generation = generation;
        CreatedUtc = createdUtc;
    }

    public string Command { get; }
    public long Generation { get; }
    public DateTime CreatedUtc { get; }
}
