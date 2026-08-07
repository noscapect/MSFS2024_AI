package com.noscapect.vfo.companion.protocol

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

const val COMPANION_PROTOCOL_VERSION = 1

@Serializable
data class CompanionState(
    val protocolVersion: Int,
    val kind: String,
    val sentUtc: String,
    val companionVersion: String,
    val connected: Boolean,
    val aircraftReady: Boolean,
    val aircraft: AircraftSummary,
    val telemetry: FlightTelemetry,
    val flow: FlowState,
    val flows: List<FlowListItem>,
    val gsx: GsxState,
)

@Serializable
data class AircraftSummary(
    val title: String,
    val family: String,
    val supported: Boolean,
    val warning: String? = null,
    val phase: String,
)

@Serializable
data class FlightTelemetry(
    val aglFeet: Double,
    val altitudeFeet: Double,
    val airspeedKnots: Double,
    val verticalSpeedFpm: Double,
)

@Serializable
data class FlowState(
    val id: String? = null,
    val name: String,
    val status: String,
    val currentStepId: String? = null,
    val currentStep: String,
    val assignedRole: String,
    val completedSteps: Int,
    val totalSteps: Int,
    val waitingFor: String,
    val guidance: String,
    val transition: String? = null,
    val canStart: Boolean,
    val canConfirm: Boolean,
    val canPause: Boolean,
    val canResume: Boolean,
    val canCancel: Boolean,
)

@Serializable
data class FlowListItem(
    val id: String,
    val name: String,
    val automationSummary: String,
    val state: String,
)

@Serializable
data class GsxState(
    val summary: String,
    val passengerOperation: String,
    val passengerProgress: String,
    val passengerPercent: Int,
    val actionRequired: String,
    val hasActionRequired: Boolean,
    val activeServices: List<String>,
    val promptTitle: String? = null,
    val choices: List<String>,
    val canOpenMenu: Boolean,
)

@Serializable
data class CompanionCommand(
    val protocolVersion: Int = COMPANION_PROTOCOL_VERSION,
    val requestId: String,
    val action: CompanionAction,
    val flowId: String? = null,
    val choiceIndex: Int? = null,
)

@Serializable
enum class CompanionAction {
    @SerialName("request_state") REQUEST_STATE,
    @SerialName("start_flow") START_FLOW,
    @SerialName("start_next_flow") START_NEXT_FLOW,
    @SerialName("gsx_open_menu") GSX_OPEN_MENU,
    @SerialName("gsx_menu_choice") GSX_MENU_CHOICE,
    @SerialName("confirm") CONFIRM,
    @SerialName("pause") PAUSE,
    @SerialName("resume") RESUME,
    @SerialName("cancel") CANCEL,
}

@Serializable
data class CommandResult(
    val protocolVersion: Int,
    val kind: String,
    val requestId: String,
    val accepted: Boolean,
    val message: String,
    val sentUtc: String,
)
