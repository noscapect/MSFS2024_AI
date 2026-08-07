package com.noscapect.vfo.companion.security

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class PairingStore(context: Context) {
    private val preferences = context.getSharedPreferences(
        "android_companion_pairing",
        Context.MODE_PRIVATE,
    )

    fun save(pairingUri: String) {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
        val encrypted = cipher.doFinal(pairingUri.toByteArray(Charsets.UTF_8))
        preferences.edit()
            .putString(KEY_IV, Base64Url.encode(cipher.iv))
            .putString(KEY_PAYLOAD, Base64Url.encode(encrypted))
            .apply()
    }

    fun load(): String? = runCatching {
        val iv = Base64Url.decode(checkNotNull(preferences.getString(KEY_IV, null)))
        val encrypted = Base64Url.decode(
            checkNotNull(preferences.getString(KEY_PAYLOAD, null))
        )
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(
            Cipher.DECRYPT_MODE,
            getOrCreateKey(),
            GCMParameterSpec(128, iv),
        )
        cipher.doFinal(encrypted).toString(Charsets.UTF_8)
    }.getOrElse {
        clear()
        null
    }

    fun clear() {
        preferences.edit().clear().apply()
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }

        val generator = KeyGenerator.getInstance(
            KeyProperties.KEY_ALGORITHM_AES,
            "AndroidKeyStore",
        )
        generator.init(
            KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build()
        )
        return generator.generateKey()
    }

    private companion object {
        const val KEY_ALIAS = "vfo_android_companion_pairing_v1"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
        const val KEY_IV = "iv"
        const val KEY_PAYLOAD = "payload"
    }
}
