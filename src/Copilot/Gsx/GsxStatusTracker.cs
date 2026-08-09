using System.Text.RegularExpressions;

namespace Msfs2024Ai.Copilot.Gsx;

/// <summary>
/// Keeps GSX's short-lived notification separate from passenger progress that
/// must survive unrelated baggage/catering notifications.
/// </summary>
internal sealed class GsxStatusTracker
{
    private static readonly Regex PassengerProgressRegex = new(
        @"\d+\s*/\s*\d+\s+passengers\s+(?:boarded|deboarded)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private IReadOnlyList<string> _notificationLines = Array.Empty<string>();
    private DateTime _notificationExpiresUtc = DateTime.MinValue;
    private string? _passengerProgressLine;

    public void Update(
        IReadOnlyList<string>? lines,
        TimeSpan lifetime,
        DateTime utcNow)
    {
        var current = (lines ?? Array.Empty<string>())
            .Select(line => (line ?? string.Empty).Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        _notificationLines = current;
        _notificationExpiresUtc = lifetime > TimeSpan.Zero
            ? utcNow.Add(lifetime)
            : utcNow;

        if (StartsNewPassengerPhase(current)
            || EndsPassengerTracking(current))
        {
            _passengerProgressLine = null;
        }

        var passengerLine = current.FirstOrDefault(
            line => PassengerProgressRegex.IsMatch(line));
        if (passengerLine != null)
        {
            _passengerProgressLine = passengerLine;
        }
    }

    public IReadOnlyList<string> CurrentNotifications(DateTime utcNow) =>
        utcNow < _notificationExpiresUtc
            ? _notificationLines
            : Array.Empty<string>();

    public IReadOnlyList<string> Snapshot(DateTime utcNow)
    {
        var notifications = CurrentNotifications(utcNow);
        if (_passengerProgressLine == null
            || notifications.Any(line => PassengerProgressRegex.IsMatch(line)))
        {
            return notifications;
        }

        return notifications.Concat(new[] { _passengerProgressLine }).ToArray();
    }

    public void Reset()
    {
        _notificationLines = Array.Empty<string>();
        _notificationExpiresUtc = DateTime.MinValue;
        _passengerProgressLine = null;
    }

    private static bool StartsNewPassengerPhase(IEnumerable<string> lines) =>
        lines.Any(line =>
            Contains(line, "boarding requested")
            || Contains(line, "deboarding requested"));

    private static bool EndsPassengerTracking(IEnumerable<string> lines) =>
        lines.Any(line =>
            Contains(line, "departure clearance requested")
            || Contains(line, "commencing push")
            || Contains(line, "have a good trip"));

    private static bool Contains(string line, string value) =>
        line.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
}
