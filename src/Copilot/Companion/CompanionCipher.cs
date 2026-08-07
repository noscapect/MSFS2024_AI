using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Msfs2024Ai.Copilot.Companion;

internal sealed class CompanionCipher
{
    public const int WireVersion = 1;
    private const int NonceSize = 12;
    private const int TagBits = 128;
    private static readonly TimeSpan MaximumClockDifference =
        TimeSpan.FromMinutes(10);

    private readonly string _sessionId;
    private readonly byte[] _key;
    private readonly string _sender;
    private readonly string _expectedSender;
    private readonly HashSet<string> _receivedMessageIds =
        new(StringComparer.Ordinal);
    private readonly Queue<string> _receivedMessageOrder = new();

    public CompanionCipher(
        string sessionId,
        byte[] pairingSecret,
        string sender)
    {
        if (pairingSecret.Length != 32)
        {
            throw new ArgumentException(
                "The pairing secret must contain exactly 32 bytes.",
                nameof(pairingSecret));
        }
        if (sender != "desktop" && sender != "tablet")
        {
            throw new ArgumentException(
                "The companion cipher sender must be desktop or tablet.",
                nameof(sender));
        }

        _sessionId = sessionId;
        _key = Derive(pairingSecret, "vfo-e2e-key-v1:" + sessionId);
        _sender = sender;
        _expectedSender = sender == "desktop" ? "tablet" : "desktop";
    }

    public string Seal(string plaintext) =>
        Seal(plaintext, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, null);

    internal string Seal(
        string plaintext,
        string messageId,
        DateTimeOffset sentUtc,
        byte[]? suppliedNonce)
    {
        var nonce = suppliedNonce ?? RandomBytes(NonceSize);
        if (nonce.Length != NonceSize)
        {
            throw new ArgumentException("The nonce must contain 12 bytes.", nameof(suppliedNonce));
        }
        var sentUnixMillis = sentUtc.ToUnixTimeMilliseconds();
        var aad = BuildAdditionalData(
            _sender,
            messageId,
            sentUnixMillis);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new ChaCha20Poly1305();
        cipher.Init(
            true,
            new AeadParameters(
                new KeyParameter(_key),
                TagBits,
                nonce,
                aad));
        var ciphertext = new byte[cipher.GetOutputSize(plaintextBytes.Length)];
        var written = cipher.ProcessBytes(
            plaintextBytes,
            0,
            plaintextBytes.Length,
            ciphertext,
            0);
        written += cipher.DoFinal(ciphertext, written);
        if (written != ciphertext.Length)
        {
            Array.Resize(ref ciphertext, written);
        }

        return CompanionProtocol.Serialize(
            new Dictionary<string, object>
            {
                ["wireVersion"] = WireVersion,
                ["sender"] = _sender,
                ["messageId"] = messageId,
                ["sentUnixMillis"] = sentUnixMillis,
                ["nonce"] = Base64Url.Encode(nonce),
                ["ciphertext"] = Base64Url.Encode(ciphertext)
            });
    }

    public bool TryOpen(string envelope, out string plaintext, out string error)
    {
        plaintext = string.Empty;
        error = string.Empty;
        IDictionary<string, object>? values;
        try
        {
            values = new JavaScriptSerializer().DeserializeObject(envelope)
                as IDictionary<string, object>;
        }
        catch
        {
            error = "The encrypted companion envelope was not valid JSON.";
            return false;
        }

        if (values == null
            || !TryReadInt(values, "wireVersion", out var wireVersion)
            || wireVersion != WireVersion
            || !string.Equals(ReadString(values, "sender"), _expectedSender, StringComparison.Ordinal)
            || !TryReadLong(values, "sentUnixMillis", out var sentUnixMillis))
        {
            error = "The encrypted companion envelope header was invalid.";
            return false;
        }
        var messageId = ReadString(values, "messageId");
        if (string.IsNullOrWhiteSpace(messageId)
            || messageId == null
            || messageId.Length > 80
            || _receivedMessageIds.Contains(messageId))
        {
            error = "The encrypted companion message was missing, invalid, or already received.";
            return false;
        }

        DateTimeOffset sentUtc;
        try
        {
            sentUtc = DateTimeOffset.FromUnixTimeMilliseconds(sentUnixMillis);
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "The encrypted companion message timestamp was invalid.";
            return false;
        }
        if ((DateTimeOffset.UtcNow - sentUtc).Duration() > MaximumClockDifference)
        {
            error = "The encrypted companion message was stale or the device clocks differ too much.";
            return false;
        }

        if (!Base64Url.TryDecode(ReadString(values, "nonce") ?? string.Empty, out var nonce)
            || nonce.Length != NonceSize
            || !Base64Url.TryDecode(
                ReadString(values, "ciphertext") ?? string.Empty,
                out var ciphertext)
            || ciphertext.Length < TagBits / 8)
        {
            error = "The encrypted companion message data was invalid.";
            return false;
        }

        try
        {
            var cipher = new ChaCha20Poly1305();
            cipher.Init(
                false,
                new AeadParameters(
                    new KeyParameter(_key),
                    TagBits,
                    nonce,
                    BuildAdditionalData(
                        _expectedSender,
                        messageId,
                        sentUnixMillis)));
            var output = new byte[cipher.GetOutputSize(ciphertext.Length)];
            var written = cipher.ProcessBytes(
                ciphertext,
                0,
                ciphertext.Length,
                output,
                0);
            written += cipher.DoFinal(output, written);
            plaintext = Encoding.UTF8.GetString(output, 0, written);
        }
        catch (InvalidCipherTextException)
        {
            error = "The encrypted companion message could not be authenticated.";
            return false;
        }

        RememberMessage(messageId);
        return true;
    }

    public static string DeriveRelayCredential(
        string sessionId,
        byte[] pairingSecret) =>
        Base64Url.Encode(Derive(
            pairingSecret,
            "vfo-relay-auth-v1:" + sessionId));

    private byte[] BuildAdditionalData(
        string sender,
        string messageId,
        long sentUnixMillis) =>
        Encoding.UTF8.GetBytes(
            $"{WireVersion}|{_sessionId}|{sender}|{messageId}|{sentUnixMillis}");

    private void RememberMessage(string messageId)
    {
        _receivedMessageIds.Add(messageId);
        _receivedMessageOrder.Enqueue(messageId);
        while (_receivedMessageOrder.Count > 512)
        {
            _receivedMessageIds.Remove(_receivedMessageOrder.Dequeue());
        }
    }

    private static byte[] Derive(byte[] secret, string purpose)
    {
        using var hmac = new HMACSHA256(secret);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(purpose));
    }

    private static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return bytes;
    }

    private static string? ReadString(
        IDictionary<string, object> values,
        string key) =>
        values.TryGetValue(key, out var value) ? value as string : null;

    private static bool TryReadInt(
        IDictionary<string, object> values,
        string key,
        out int result)
    {
        result = 0;
        try
        {
            if (!values.TryGetValue(key, out var value) || value == null)
            {
                return false;
            }
            result = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadLong(
        IDictionary<string, object> values,
        string key,
        out long result)
    {
        result = 0;
        try
        {
            if (!values.TryGetValue(key, out var value) || value == null)
            {
                return false;
            }
            result = Convert.ToInt64(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
