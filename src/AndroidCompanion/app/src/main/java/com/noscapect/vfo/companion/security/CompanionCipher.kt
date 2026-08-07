package com.noscapect.vfo.companion.security

import java.nio.charset.StandardCharsets
import java.security.GeneralSecurityException
import java.security.SecureRandom
import java.util.UUID
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.Mac
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec
import kotlinx.serialization.Serializable
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json

class CompanionCipher(
    private val sessionId: String,
    pairingSecret: ByteArray,
    private val sender: String,
) {
    private val json = Json { ignoreUnknownKeys = true }
    private val key = derive(pairingSecret, "vfo-e2e-key-v1:$sessionId")
    private val expectedSender = if (sender == "desktop") "tablet" else "desktop"
    private val receivedIds = LinkedHashSet<String>()

    init {
        require(pairingSecret.size == 32) { "Pairing secret must contain 32 bytes." }
        require(sender == "desktop" || sender == "tablet") {
            "Companion sender must be desktop or tablet."
        }
    }

    fun seal(plaintext: String): String = seal(
        plaintext = plaintext,
        messageId = UUID.randomUUID().toString().replace("-", ""),
        sentUnixMillis = System.currentTimeMillis(),
        nonce = ByteArray(NONCE_SIZE).also(SecureRandom()::nextBytes),
    )

    internal fun seal(
        plaintext: String,
        messageId: String,
        sentUnixMillis: Long,
        nonce: ByteArray,
    ): String {
        require(nonce.size == NONCE_SIZE) { "Nonce must contain 12 bytes." }
        val cipher = Cipher.getInstance("ChaCha20-Poly1305")
        cipher.init(
            Cipher.ENCRYPT_MODE,
            SecretKeySpec(key, "ChaCha20"),
            IvParameterSpec(nonce),
        )
        cipher.updateAAD(additionalData(sender, messageId, sentUnixMillis))
        val ciphertext = cipher.doFinal(plaintext.toByteArray(StandardCharsets.UTF_8))
        return json.encodeToString(
            EncryptedEnvelope(
                sender = sender,
                messageId = messageId,
                sentUnixMillis = sentUnixMillis,
                nonce = Base64Url.encode(nonce),
                ciphertext = Base64Url.encode(ciphertext),
            )
        )
    }

    @Synchronized
    fun open(envelopeJson: String): Result<String> = runCatching {
        val envelope = json.decodeFromString<EncryptedEnvelope>(envelopeJson)
        require(envelope.wireVersion == WIRE_VERSION) { "Unsupported encrypted wire version." }
        require(envelope.sender == expectedSender) { "Unexpected encrypted message sender." }
        require(envelope.messageId.isNotBlank() && envelope.messageId.length <= 80) {
            "Invalid encrypted message ID."
        }
        require(envelope.messageId !in receivedIds) { "Encrypted message was already received." }
        require(kotlin.math.abs(System.currentTimeMillis() - envelope.sentUnixMillis) <= MAX_CLOCK_DIFFERENCE_MS) {
            "Encrypted message is stale or the device clocks differ too much."
        }

        val nonce = Base64Url.decode(envelope.nonce)
        val ciphertext = Base64Url.decode(envelope.ciphertext)
        require(nonce.size == NONCE_SIZE && ciphertext.size >= TAG_SIZE) {
            "Invalid encrypted message data."
        }
        val cipher = Cipher.getInstance("ChaCha20-Poly1305")
        cipher.init(
            Cipher.DECRYPT_MODE,
            SecretKeySpec(key, "ChaCha20"),
            IvParameterSpec(nonce),
        )
        cipher.updateAAD(
            additionalData(
                expectedSender,
                envelope.messageId,
                envelope.sentUnixMillis,
            )
        )
        val plaintext = cipher.doFinal(ciphertext).toString(StandardCharsets.UTF_8)
        receivedIds += envelope.messageId
        while (receivedIds.size > MAX_RECEIVED_IDS) {
            receivedIds.remove(receivedIds.first())
        }
        plaintext
    }

    private fun additionalData(
        messageSender: String,
        messageId: String,
        sentUnixMillis: Long,
    ): ByteArray =
        "$WIRE_VERSION|$sessionId|$messageSender|$messageId|$sentUnixMillis"
            .toByteArray(StandardCharsets.UTF_8)

    companion object {
        const val WIRE_VERSION = 1
        private const val NONCE_SIZE = 12
        private const val TAG_SIZE = 16
        private const val MAX_CLOCK_DIFFERENCE_MS = 10 * 60 * 1000L
        private const val MAX_RECEIVED_IDS = 512

        fun deriveRelayCredential(sessionId: String, secret: ByteArray): String {
            require(secret.size == 32) { "Pairing secret must contain 32 bytes." }
            return Base64Url.encode(derive(secret, "vfo-relay-auth-v1:$sessionId"))
        }

        private fun derive(secret: ByteArray, purpose: String): ByteArray {
            val mac = Mac.getInstance("HmacSHA256")
            mac.init(SecretKeySpec(secret, "HmacSHA256"))
            return mac.doFinal(purpose.toByteArray(StandardCharsets.UTF_8))
        }
    }
}

@Serializable
private data class EncryptedEnvelope(
    val wireVersion: Int = CompanionCipher.WIRE_VERSION,
    val sender: String,
    val messageId: String,
    val sentUnixMillis: Long,
    val nonce: String,
    val ciphertext: String,
)

object Base64Url {
    fun encode(value: ByteArray): String =
        Base64.getUrlEncoder().withoutPadding().encodeToString(value)

    fun decode(value: String): ByteArray {
        require(value.isNotBlank() && value.all { it.isLetterOrDigit() || it == '-' || it == '_' }) {
            "Invalid base64url value."
        }
        return try {
            Base64.getUrlDecoder().decode(value)
        } catch (exception: IllegalArgumentException) {
            throw GeneralSecurityException("Invalid base64url value.", exception)
        }
    }
}
