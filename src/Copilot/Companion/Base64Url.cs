namespace Msfs2024Ai.Copilot.Companion;

internal static class Base64Url
{
    public static string Encode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static bool TryDecode(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character =>
                !char.IsLetterOrDigit(character)
                && character != '-'
                && character != '_'))
        {
            return false;
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
