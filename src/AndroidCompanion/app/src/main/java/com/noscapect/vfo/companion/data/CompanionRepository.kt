package com.noscapect.vfo.companion.data

import com.noscapect.vfo.companion.protocol.CommandResult
import com.noscapect.vfo.companion.protocol.CompanionAction
import com.noscapect.vfo.companion.protocol.CompanionCommand
import com.noscapect.vfo.companion.protocol.CompanionState
import java.util.UUID
import kotlinx.coroutines.flow.StateFlow

enum class ConnectionStatus {
    UNPAIRED,
    CONNECTING,
    CONNECTED_LOCAL,
    CONNECTED_RELAY,
    DISCONNECTED,
}

data class CompanionConnection(
    val status: ConnectionStatus = ConnectionStatus.UNPAIRED,
    val message: String = "Pair this tablet with Copilot.exe",
    val lastStateReceivedAtMillis: Long? = null,
    val controlsAllowed: Boolean = false,
)

interface CompanionRepository {
    val connection: StateFlow<CompanionConnection>
    val state: StateFlow<CompanionState?>
    val lastCommandResult: StateFlow<CommandResult?>

    fun connect(pairingUri: String)
    fun savedPairingUri(): String?
    fun disconnect()
    fun send(command: CompanionCommand)

    fun send(action: CompanionAction, flowId: String? = null, choiceIndex: Int? = null) =
        send(
            CompanionCommand(
                requestId = "android-${UUID.randomUUID()}",
                action = action,
                flowId = flowId,
                choiceIndex = choiceIndex,
            )
        )
}
