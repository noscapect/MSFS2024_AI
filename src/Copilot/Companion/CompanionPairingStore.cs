using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.Companion;

internal static class CompanionPairingStore
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("MSFS2024_AI Android Companion Pairing v1");
    private static readonly string PairingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MSFS2024_AI",
        "android-companion-pairing.dat");

    public static void Save(CompanionPairing pairing)
    {
        var json = new JavaScriptSerializer().Serialize(pairing);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            Entropy,
            DataProtectionScope.CurrentUser);
        var directory = Path.GetDirectoryName(PairingPath)
                        ?? throw new InvalidOperationException(
                            "Could not resolve the companion pairing directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(PairingPath, protectedBytes);
    }

    public static bool TryLoad(out CompanionPairing? pairing)
    {
        pairing = null;
        try
        {
            if (!File.Exists(PairingPath))
            {
                return false;
            }
            var json = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(
                    File.ReadAllBytes(PairingPath),
                    Entropy,
                    DataProtectionScope.CurrentUser));
            var value = new JavaScriptSerializer().Deserialize<CompanionPairing>(json);
            if (value == null
                || !RelayCompanionOptions.TryFromPairing(value, out _, out _))
            {
                return false;
            }
            pairing = value;
            return true;
        }
        catch (Exception exception) when (
            exception is CryptographicException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            return false;
        }
    }

    public static void Revoke()
    {
        if (File.Exists(PairingPath))
        {
            File.Delete(PairingPath);
        }
    }
}
