package com.noscapect.vfo.companion

import androidx.lifecycle.ViewModel
import com.noscapect.vfo.companion.data.CompanionConnection
import com.noscapect.vfo.companion.data.CompanionRepository
import com.noscapect.vfo.companion.protocol.CommandResult
import com.noscapect.vfo.companion.protocol.CompanionAction
import com.noscapect.vfo.companion.protocol.CompanionState
import kotlinx.coroutines.flow.StateFlow

class CompanionViewModel(
    private val repository: CompanionRepository,
) : ViewModel() {
    val connection: StateFlow<CompanionConnection> = repository.connection
    val state: StateFlow<CompanionState?> = repository.state
    val lastCommandResult: StateFlow<CommandResult?> = repository.lastCommandResult
    val savedPairingUri: String? get() = repository.savedPairingUri()

    fun connect(pairingUri: String): Result<Unit> = runCatching {
        repository.connect(pairingUri)
    }

    fun startNextFlow() = repository.send(CompanionAction.START_NEXT_FLOW)
    fun startFlow(flowId: String) =
        repository.send(CompanionAction.START_FLOW, flowId = flowId)
    fun confirm() = repository.send(CompanionAction.CONFIRM)
    fun pause() = repository.send(CompanionAction.PAUSE)
    fun resume() = repository.send(CompanionAction.RESUME)
    fun cancel() = repository.send(CompanionAction.CANCEL)
    fun openGsxMenu() = repository.send(CompanionAction.GSX_OPEN_MENU)
    fun chooseGsx(index: Int) =
        repository.send(CompanionAction.GSX_MENU_CHOICE, choiceIndex = index)

    override fun onCleared() {
        repository.disconnect()
    }
}
