namespace Msfs2024Ai.Copilot.Companion;

internal sealed class RelayCompanionOptions
{
    private const string EnableVariable = "VFO_COMPANION_DEVELOPMENT";
    private const string RelayVariable = "VFO_COMPANION_RELAY";
    private const string SessionVariable = "VFO_COMPANION_SESSION";
    private const string SecretVariable = "VFO_COMPANION_SECRET";
    private const string AllowControlVariable = "VFO_COMPANION_ALLOW_CONTROL";

    private RelayCompanionOptions(
        Uri endpoint,
        string sessionId,
        byte[] pairingSecret,
        bool controlsAllowed)
    {
        Endpoint = endpoint;
        SessionId = sessionId;
        PairingSecret = pairingSecret;
        ControlsAllowed = controlsAllowed;
    }

    public Uri Endpoint { get; }
    public string SessionId { get; }
    public byte[] PairingSecret { get; }
    public string RelayCredential =>
        CompanionCipher.DeriveRelayCredential(SessionId, PairingSecret);
    public bool ControlsAllowed { get; }

    public Uri DesktopWebSocketUri
    {
        get
        {
            var builder = new UriBuilder(Endpoint);
            builder.Path = builder.Path.TrimEnd('/')
                           + "/v1/session/"
                           + Uri.EscapeDataString(SessionId);
            builder.Query = "role=desktop";
            return builder.Uri;
        }
    }

    public static bool TryFromEnvironment(
        out RelayCompanionOptions? options,
        out string error)
    {
        options = null;
        error = string.Empty;
        if (!string.Equals(
                Environment.GetEnvironmentVariable(EnableVariable),
                "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        var relay = Environment.GetEnvironmentVariable(RelayVariable)?.Trim();
        var session = Environment.GetEnvironmentVariable(SessionVariable)?.Trim();
        var secretText = Environment.GetEnvironmentVariable(SecretVariable)?.Trim();
        if (!Uri.TryCreate(relay, UriKind.Absolute, out var endpoint)
            || !string.Equals(endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            error = $"{RelayVariable} must be an absolute wss:// URI.";
            return false;
        }
        if (session == null
            || string.IsNullOrWhiteSpace(session)
            || session.Length < 20
            || session.Length > 80
            || session.Any(character =>
                !char.IsLetterOrDigit(character)
                && character != '_'
                && character != '-'))
        {
            error = $"{SessionVariable} must contain 20-80 URL-safe characters.";
            return false;
        }
        if (secretText == null
            || !Base64Url.TryDecode(secretText, out var pairingSecret)
            || pairingSecret.Length != 32)
        {
            error = $"{SecretVariable} must be a base64url-encoded 32-byte secret.";
            return false;
        }

        return TryCreate(
            endpoint,
            session,
            pairingSecret,
            string.Equals(
                Environment.GetEnvironmentVariable(AllowControlVariable),
                "1",
                StringComparison.Ordinal),
            out options,
            out error);
    }

    public static bool TryFromPairing(
        CompanionPairing pairing,
        out RelayCompanionOptions? options,
        out string error)
    {
        options = null;
        error = string.Empty;
        if (!Uri.TryCreate(
                pairing.RelayEndpoint,
                UriKind.Absolute,
                out var endpoint)
            || !Base64Url.TryDecode(
                pairing.PairingSecret,
                out var pairingSecret))
        {
            error = "The saved Android companion pairing is invalid.";
            return false;
        }
        return TryCreate(
            endpoint,
            pairing.SessionId,
            pairingSecret,
            pairing.ControlsAllowed,
            out options,
            out error);
    }

    private static bool TryCreate(
        Uri endpoint,
        string session,
        byte[] pairingSecret,
        bool controlsAllowed,
        out RelayCompanionOptions? options,
        out string error)
    {
        options = null;
        error = string.Empty;
        if (!string.Equals(endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            error = "The Android companion relay must use wss://.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(session)
            || session.Length < 20
            || session.Length > 80
            || session.Any(character =>
                !char.IsLetterOrDigit(character)
                && character != '_'
                && character != '-'))
        {
            error = "The Android companion session ID is invalid.";
            return false;
        }
        if (pairingSecret.Length != 32)
        {
            error = "The Android companion pairing secret is invalid.";
            return false;
        }

        options = new RelayCompanionOptions(
            endpoint,
            session,
            pairingSecret,
            controlsAllowed);
        return true;
    }
}
