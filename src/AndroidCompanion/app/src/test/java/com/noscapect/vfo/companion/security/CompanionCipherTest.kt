package com.noscapect.vfo.companion.security

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Test

class CompanionCipherTest {
    private val secret = ByteArray(32) { it.toByte() }

    @Test
    fun matchesCrossPlatformChaCha20Poly1305Vector() {
        val cipher = CompanionCipher(SESSION, secret, "desktop")
        val envelope = cipher.seal(
            plaintext = "{\"kind\":\"state\"}",
            messageId = "00112233445566778899aabbccddeeff",
            sentUnixMillis = 1785854400000,
            nonce = ByteArray(12) { it.toByte() },
        )
        val values = Json.parseToJsonElement(envelope).jsonObject

        assertEquals("AAECAwQFBgcICQoL", values.getValue("nonce").jsonPrimitive.content)
        assertEquals(
            "j8Ufwl6bvEwinuM7UR_NlbeLMcVDqhZ58_FF3qb78Uc",
            values.getValue("ciphertext").jsonPrimitive.content,
        )
        assertEquals(
            "Qc5fX5NDx5wmI_CPTqiVSVZq4P91VM9bsHdLKZWAGK4",
            CompanionCipher.deriveRelayCredential(SESSION, secret),
        )
    }

    private companion object {
        const val SESSION = "abcdefghijklmnopqrstuvwx"
    }
}
