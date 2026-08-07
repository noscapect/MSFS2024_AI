package com.noscapect.vfo.companion.data

import com.noscapect.vfo.companion.security.CompanionCipher
import java.io.DataInputStream
import java.io.DataOutputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean

data class LocalEndpoint(val host: String, val port: Int)

class LocalCompanionClient(
    private val endpoints: List<LocalEndpoint>,
    private val cipher: CompanionCipher,
    private val onConnected: () -> Unit,
    private val onMessage: (String) -> Unit,
    private val onUnavailable: () -> Unit,
    private val onClosed: () -> Unit,
) : AutoCloseable {
    private val closed = AtomicBoolean(false)
    private val sendLock = Any()
    @Volatile private var socket: Socket? = null
    @Volatile private var output: DataOutputStream? = null
    @Volatile private var connected = false

    fun start() {
        Thread(::connectAndReceive, "vfo-companion-lan").apply {
            isDaemon = true
            start()
        }
    }

    fun send(plaintext: String): Boolean {
        if (!connected || closed.get()) return false
        val envelope = cipher.seal(plaintext).toByteArray(Charsets.UTF_8)
        if (envelope.size > MAX_FRAME_BYTES) return false
        Thread({
            runCatching {
                synchronized(sendLock) {
                    val stream = checkNotNull(output)
                    stream.writeInt(envelope.size)
                    stream.write(envelope)
                    stream.flush()
                }
            }.onFailure { close() }
        }, "vfo-companion-lan-send").apply {
            isDaemon = true
            start()
        }
        return true
    }

    private fun connectAndReceive() {
        var selected: Socket? = null
        for (endpoint in endpoints) {
            if (closed.get()) return
            val candidate = Socket()
            try {
                candidate.tcpNoDelay = true
                candidate.connect(InetSocketAddress(endpoint.host, endpoint.port), CONNECT_TIMEOUT_MS)
                selected = candidate
                break
            } catch (_: Exception) {
                candidate.close()
            }
        }
        if (selected == null || closed.get()) {
            selected?.close()
            if (!closed.get()) onUnavailable()
            return
        }

        socket = selected
        output = DataOutputStream(selected.getOutputStream())
        connected = true
        onConnected()
        try {
            val input = DataInputStream(selected.getInputStream())
            while (!closed.get()) {
                val length = input.readInt()
                require(length in 1..MAX_FRAME_BYTES) { "Invalid LAN frame length." }
                val bytes = ByteArray(length)
                input.readFully(bytes)
                val envelope = bytes.toString(Charsets.UTF_8)
                val plaintext = cipher.open(envelope).getOrThrow()
                onMessage(plaintext)
            }
        } catch (_: Exception) {
        } finally {
            connected = false
            output = null
            selected.close()
            socket = null
            if (!closed.get()) onClosed()
        }
    }

    override fun close() {
        if (!closed.compareAndSet(false, true)) return
        connected = false
        synchronized(sendLock) {
            output = null
            socket?.close()
            socket = null
        }
    }

    private companion object {
        const val CONNECT_TIMEOUT_MS = 1_200
        const val MAX_FRAME_BYTES = 256 * 1024
    }
}
