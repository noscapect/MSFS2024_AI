package com.noscapect.vfo.companion.data

import android.content.Context
import android.net.Uri
import com.noscapect.vfo.companion.protocol.CommandResult
import com.noscapect.vfo.companion.protocol.CompanionAction
import com.noscapect.vfo.companion.protocol.CompanionCommand
import com.noscapect.vfo.companion.protocol.CompanionState
import com.noscapect.vfo.companion.security.Base64Url
import com.noscapect.vfo.companion.security.CompanionCipher
import com.noscapect.vfo.companion.security.PairingStore
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener

class WebSocketCompanionRepository(context: Context) : CompanionRepository {
    private val json = Json { ignoreUnknownKeys = true }
    private val client = OkHttpClient.Builder()
        .pingInterval(20, TimeUnit.SECONDS)
        .retryOnConnectionFailure(true)
        .build()
    private val pairingStore = PairingStore(context)

    private val mutableConnection = MutableStateFlow(CompanionConnection())
    override val connection: StateFlow<CompanionConnection> = mutableConnection

    private val mutableState = MutableStateFlow<CompanionState?>(null)
    override val state: StateFlow<CompanionState?> = mutableState

    private val mutableLastCommandResult = MutableStateFlow<CommandResult?>(null)
    override val lastCommandResult: StateFlow<CommandResult?> = mutableLastCommandResult

    private var socket: WebSocket? = null
    private var localClient: LocalCompanionClient? = null
    private var cipher: CompanionCipher? = null
    private var controlsAllowed = false

    override fun connect(pairingUri: String) {
        val pairing = Uri.parse(pairingUri.trim())
        require(pairing.scheme == "vfo" && pairing.host == "pair") {
            "That is not a Virtual First Officer pairing code."
        }
        val endpoint = pairing.getQueryParameter("relay")
            ?: error("The pairing code does not contain a relay address.")
        val sessionId = pairing.getQueryParameter("session")
            ?: error("The pairing code does not contain a session ID.")
        val secretText = pairing.getQueryParameter("secret")
            ?: error("The pairing code does not contain a pairing secret.")
        val secret = Base64Url.decode(secretText)
        require(secret.size == 32) { "The pairing secret is invalid." }
        require(endpoint.startsWith("wss://")) {
            "The relay must use an encrypted wss:// connection."
        }

        cipher = CompanionCipher(sessionId, secret, "tablet")
        controlsAllowed = pairing.getQueryParameter("controls") == "1"
        val relayCredential = CompanionCipher.deriveRelayCredential(sessionId, secret)
        pairingStore.save(pairingUri.trim())
        socket?.close(1000, "Replaced by a new connection")
        socket = null
        localClient?.close()
        localClient = null
        mutableConnection.value = CompanionConnection(
            ConnectionStatus.CONNECTING,
            "Connecting to the paired PC...",
        )

        val localEndpoints = pairing.getQueryParameters("lan").mapNotNull(::parseLocalEndpoint)
        if (localEndpoints.isEmpty()) {
            connectRelay(endpoint, sessionId, relayCredential)
            return
        }

        val localCipher = checkNotNull(cipher)
        val candidate = LocalCompanionClient(
            endpoints = localEndpoints,
            cipher = localCipher,
            onConnected = {
                mutableConnection.value = CompanionConnection(
                    ConnectionStatus.CONNECTED_LOCAL,
                    if (controlsAllowed) {
                        "Connected directly on LAN"
                    } else {
                        "Encrypted LAN - view only"
                    },
                    controlsAllowed = controlsAllowed,
                )
                requestState()
            },
            onMessage = ::processIncomingMessage,
            onUnavailable = {
                localClient = null
                connectRelay(endpoint, sessionId, relayCredential)
            },
            onClosed = {
                localClient = null
                mutableConnection.value = CompanionConnection(
                    ConnectionStatus.CONNECTING,
                    "LAN connection lost; switching to relay...",
                )
                connectRelay(endpoint, sessionId, relayCredential)
            },
        )
        localClient = candidate
        candidate.start()
    }

    override fun savedPairingUri(): String? = pairingStore.load()

    override fun disconnect() {
        socket?.close(1000, "Disconnected by pilot")
        socket = null
        localClient?.close()
        localClient = null
        mutableConnection.value = CompanionConnection(
            ConnectionStatus.DISCONNECTED,
            "Disconnected",
        )
    }

    override fun send(command: CompanionCommand) {
        if (!controlsAllowed && command.action != CompanionAction.REQUEST_STATE) {
            mutableConnection.value = mutableConnection.value.copy(
                message = "This development pairing is view-only.",
            )
            return
        }

        val currentCipher = cipher
        val plaintext = json.encodeToString(command)
        val sent = when {
            currentCipher == null -> false
            localClient?.send(plaintext) == true -> true
            socket?.send(currentCipher.seal(plaintext)) == true -> true
            else -> false
        }
        if (!sent) {
            mutableConnection.value = mutableConnection.value.copy(
                status = ConnectionStatus.DISCONNECTED,
                message = "Command was not sent; reconnect to the paired PC.",
            )
        }
    }

    private inner class Listener : WebSocketListener() {
        override fun onOpen(webSocket: WebSocket, response: Response) {
            mutableConnection.value = CompanionConnection(
                ConnectionStatus.CONNECTED_RELAY,
                if (controlsAllowed) {
                    "Connected securely through relay"
                } else {
                    "Encrypted relay - view only"
                },
                controlsAllowed = controlsAllowed,
            )
            requestState()
        }

        override fun onMessage(webSocket: WebSocket, text: String) {
            val plaintext = checkNotNull(cipher).open(text).getOrElse {
                mutableConnection.value = mutableConnection.value.copy(
                    message = "Received a message that failed encryption validation.",
                )
                return
            }
            processIncomingMessage(plaintext)
        }

        override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
            if (socket !== webSocket) return
            socket = null
            mutableConnection.value = CompanionConnection(
                ConnectionStatus.DISCONNECTED,
                "Connection closed${reason.takeIf { it.isNotBlank() }?.let { ": $it" } ?: ""}",
            )
        }

        override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
            if (socket !== webSocket) return
            socket = null
            mutableConnection.value = CompanionConnection(
                ConnectionStatus.DISCONNECTED,
                t.message ?: "Could not connect to the paired PC.",
            )
        }
    }

    private fun connectRelay(endpoint: String, sessionId: String, relayCredential: String) {
        if (socket != null) return
        val request = Request.Builder()
            .url("${endpoint.trimEnd('/')}/v1/session/$sessionId?role=tablet")
            .header("Authorization", "Bearer $relayCredential")
            .build()
        socket = client.newWebSocket(request, Listener())
    }

    private fun requestState() {
        send(
            CompanionCommand(
                requestId = "state-${UUID.randomUUID()}",
                action = CompanionAction.REQUEST_STATE,
            )
        )
    }

    private fun processIncomingMessage(plaintext: String) {
        runCatching {
            val element = json.parseToJsonElement(plaintext).jsonObject
            when (element["kind"]?.toString()?.trim('"')) {
                "state" -> {
                    mutableState.value = json.decodeFromString<CompanionState>(plaintext)
                    mutableConnection.value = mutableConnection.value.copy(
                        lastStateReceivedAtMillis = System.currentTimeMillis(),
                    )
                }
                "commandResult" -> {
                    mutableLastCommandResult.value =
                        json.decodeFromString<CommandResult>(plaintext)
                }
            }
        }.onFailure {
            mutableConnection.value = mutableConnection.value.copy(
                message = "Received an incompatible companion message.",
            )
        }
    }

    private fun parseLocalEndpoint(value: String): LocalEndpoint? {
        val separator = value.lastIndexOf(':')
        if (separator <= 0) return null
        val host = value.substring(0, separator)
        val port = value.substring(separator + 1).toIntOrNull() ?: return null
        if (port !in 1..65535) return null
        return LocalEndpoint(host, port)
    }
}
