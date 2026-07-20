namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxMenuSnapshot
{
    public GsxMenuSnapshot(string title, IReadOnlyList<string> choices)
    {
        Title = title;
        Choices = choices;
    }

    public string Title { get; }
    public IReadOnlyList<string> Choices { get; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && Choices.Count == 0;

    public static GsxMenuSnapshot Parse(IEnumerable<string>? lines)
    {
        var content = (lines ?? Array.Empty<string>())
            .Select(line => (line ?? string.Empty).Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        return content.Length == 0
            ? new GsxMenuSnapshot(string.Empty, Array.Empty<string>())
            : new GsxMenuSnapshot(content[0], content.Skip(1).ToArray());
    }
}
