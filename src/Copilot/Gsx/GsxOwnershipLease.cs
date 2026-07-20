using System.Globalization;

namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxOwnershipLease
{
    private static readonly TimeSpan RecoveryWindow = TimeSpan.FromHours(24);
    private readonly string _path;

    public GsxOwnershipLease()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MSFS2024_AI",
            "gsx-remote-control.lease"))
    {
    }

    internal GsxOwnershipLease(string path)
    {
        _path = path;
    }

    public bool CanRecover(DateTime utcNow)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return false;
            }

            var value = File.ReadAllText(_path).Trim();
            return DateTime.TryParseExact(
                       value,
                       "O",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                       out var writtenUtc)
                   && utcNow >= writtenUtc
                   && utcNow - writtenUtc <= RecoveryWindow;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void MarkOwned(DateTime utcNow)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(_path, utcNow.ToUniversalTime().ToString("O"));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
