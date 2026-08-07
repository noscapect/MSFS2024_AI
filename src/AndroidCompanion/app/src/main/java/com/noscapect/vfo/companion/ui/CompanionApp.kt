package com.noscapect.vfo.companion.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.noscapect.vfo.companion.CompanionViewModel
import com.noscapect.vfo.companion.data.ConnectionStatus
import com.noscapect.vfo.companion.protocol.CompanionState
import com.noscapect.vfo.companion.protocol.FlowListItem
import com.noscapect.vfo.companion.protocol.GsxState
import com.google.android.gms.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.android.gms.mlkit.vision.codescanner.GmsBarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import kotlin.math.roundToInt

private val Night = Color(0xFF081119)
private val Panel = Color(0xFF10212D)
private val PanelRaised = Color(0xFF17303F)
private val Cyan = Color(0xFF62D5E8)
private val Green = Color(0xFF68D391)
private val Amber = Color(0xFFF6C453)
private val Red = Color(0xFFFF7B72)
private val TextPrimary = Color(0xFFEAF4F8)
private val TextSecondary = Color(0xFFAAC0CB)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CompanionApp(
    viewModel: CompanionViewModel,
    initialPairingUri: String?,
) {
    val connection by viewModel.connection.collectAsStateWithLifecycle()
    val state by viewModel.state.collectAsStateWithLifecycle()
    val commandResult by viewModel.lastCommandResult.collectAsStateWithLifecycle()
    var pairingError by remember { mutableStateOf<String?>(null) }
    val context = LocalContext.current
    val scanner = remember {
        val options = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .enableAutoZoom()
            .build()
        GmsBarcodeScanning.getClient(context, options)
    }

    LaunchedEffect(initialPairingUri) {
        if (!initialPairingUri.isNullOrBlank()) {
            viewModel.connect(initialPairingUri).onFailure {
                pairingError = it.message
            }
        }
    }

    VfoTheme {
        Scaffold(
            containerColor = Night,
            topBar = {
                TopAppBar(
                    title = {
                        Column {
                            Text("VIRTUAL FIRST OFFICER", fontWeight = FontWeight.Bold)
                            Text(
                                state?.aircraft?.family ?: "Android companion",
                                style = MaterialTheme.typography.labelMedium,
                                color = TextSecondary,
                            )
                        }
                    },
                    actions = {
                        ConnectionBadge(connection.status, connection.message)
                    },
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = Night,
                        titleContentColor = TextPrimary,
                    ),
                )
            },
        ) { padding ->
            if (state == null) {
                PairingScreen(
                    modifier = Modifier.padding(padding),
                    connecting = connection.status == ConnectionStatus.CONNECTING,
                    message = pairingError ?: connection.message,
                    onConnect = { value ->
                        pairingError = null
                        viewModel.connect(value).onFailure { pairingError = it.message }
                    },
                    onScan = {
                        pairingError = null
                        scanner.startScan()
                            .addOnSuccessListener { barcode ->
                                val value = barcode.rawValue
                                if (value == null) {
                                    pairingError = "The QR code did not contain a pairing URI."
                                } else {
                                    viewModel.connect(value).onFailure {
                                        pairingError = it.message
                                    }
                                }
                            }
                            .addOnFailureListener { pairingError = it.message }
                    },
                )
            } else {
                Dashboard(
                    modifier = Modifier.padding(padding),
                    state = state!!,
                    controlsAllowed = connection.controlsAllowed,
                    resultMessage = commandResult?.message,
                    onStartNext = viewModel::startNextFlow,
                    onStartFlow = viewModel::startFlow,
                    onConfirm = viewModel::confirm,
                    onPause = viewModel::pause,
                    onResume = viewModel::resume,
                    onCancel = viewModel::cancel,
                    onOpenGsx = viewModel::openGsxMenu,
                    onGsxChoice = viewModel::chooseGsx,
                )
            }
        }
    }
}

@Composable
private fun PairingScreen(
    modifier: Modifier,
    connecting: Boolean,
    message: String,
    onConnect: (String) -> Unit,
    onScan: () -> Unit,
) {
    var code by remember { mutableStateOf("") }
    Box(modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Card(
            modifier = Modifier.widthIn(max = 620.dp).padding(24.dp),
            colors = CardDefaults.cardColors(containerColor = Panel),
        ) {
            Column(
                Modifier.padding(28.dp),
                verticalArrangement = Arrangement.spacedBy(18.dp),
            ) {
                Text("Pair this tablet", style = MaterialTheme.typography.headlineMedium)
                Text(
                    "Open Android Companion in Copilot.exe and scan its QR code. " +
                        "During foundation testing, the pairing URI can be pasted below.",
                    color = TextSecondary,
                )
                OutlinedTextField(
                    value = code,
                    onValueChange = { code = it },
                    label = { Text("Pairing URI") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
                Button(
                    onClick = onScan,
                    enabled = !connecting,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                ) {
                    Text("SCAN QR CODE")
                }
                OutlinedButton(
                    onClick = { onConnect(code) },
                    enabled = code.isNotBlank() && !connecting,
                    modifier = Modifier.fillMaxWidth().height(54.dp),
                ) {
                    if (connecting) CircularProgressIndicator(modifier = Modifier.height(24.dp))
                    else Text("USE PASTED URI")
                }
                Text(message, color = if (message.contains("not", true)) Red else TextSecondary)
            }
        }
    }
}

@Composable
private fun Dashboard(
    modifier: Modifier,
    state: CompanionState,
    controlsAllowed: Boolean,
    resultMessage: String?,
    onStartNext: () -> Unit,
    onStartFlow: (String) -> Unit,
    onConfirm: () -> Unit,
    onPause: () -> Unit,
    onResume: () -> Unit,
    onCancel: () -> Unit,
    onOpenGsx: () -> Unit,
    onGsxChoice: (Int) -> Unit,
) {
    var cancelPrompt by remember { mutableStateOf(false) }
    BoxWithConstraints(modifier.fillMaxSize()) {
        if (maxWidth >= 840.dp) {
            Row(
                Modifier.fillMaxSize().padding(16.dp),
                horizontalArrangement = Arrangement.spacedBy(16.dp),
            ) {
                LazyColumn(
                    modifier = Modifier.weight(1.65f).fillMaxHeight(),
                    verticalArrangement = Arrangement.spacedBy(14.dp),
                ) {
                    item { AircraftCard(state) }
                    item {
                        CurrentFlowCard(
                            state = state,
                            controlsAllowed = controlsAllowed,
                            resultMessage = resultMessage,
                            onStartNext = onStartNext,
                            onConfirm = onConfirm,
                            onPause = onPause,
                            onResume = onResume,
                            onCancel = { cancelPrompt = true },
                        )
                    }
                    item { GsxCard(state.gsx, controlsAllowed, onOpenGsx, onGsxChoice) }
                }
                FlowList(
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    flows = state.flows,
                    canStart = state.flow.canStart && controlsAllowed,
                    onStartFlow = onStartFlow,
                )
            }
        } else {
            LazyColumn(
                Modifier.fillMaxSize().padding(12.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                item { AircraftCard(state) }
                item {
                    CurrentFlowCard(
                        state = state,
                        controlsAllowed = controlsAllowed,
                        resultMessage = resultMessage,
                        onStartNext = onStartNext,
                        onConfirm = onConfirm,
                        onPause = onPause,
                        onResume = onResume,
                        onCancel = { cancelPrompt = true },
                    )
                }
                item { GsxCard(state.gsx, controlsAllowed, onOpenGsx, onGsxChoice) }
                item {
                    FlowList(
                        modifier = Modifier.fillMaxWidth(),
                        flows = state.flows,
                        canStart = state.flow.canStart && controlsAllowed,
                        onStartFlow = onStartFlow,
                    )
                }
            }
        }
    }

    if (cancelPrompt) {
        AlertDialog(
            onDismissRequest = { cancelPrompt = false },
            title = { Text("Cancel the active flow?") },
            text = { Text("The Windows companion remains authoritative and will stop the current flow.") },
            confirmButton = {
                TextButton(onClick = { cancelPrompt = false; onCancel() }) {
                    Text("CANCEL FLOW", color = Red)
                }
            },
            dismissButton = {
                TextButton(onClick = { cancelPrompt = false }) { Text("KEEP RUNNING") }
            },
        )
    }
}

@Composable
private fun AircraftCard(state: CompanionState) {
    PanelCard {
        Row(
            Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column {
                Text(state.aircraft.title, style = MaterialTheme.typography.titleLarge)
                Text(state.aircraft.phase.uppercase(), color = Cyan)
            }
            Row(horizontalArrangement = Arrangement.spacedBy(24.dp)) {
                TelemetryValue("IAS", state.telemetry.airspeedKnots.roundToInt(), "KT")
                TelemetryValue("ALT", state.telemetry.altitudeFeet.roundToInt(), "FT")
                TelemetryValue("AGL", state.telemetry.aglFeet.roundToInt(), "FT")
                TelemetryValue("V/S", state.telemetry.verticalSpeedFpm.roundToInt(), "FPM")
            }
        }
        state.aircraft.warning?.let { Text(it, color = Amber, modifier = Modifier.padding(top = 12.dp)) }
    }
}

@Composable
private fun CurrentFlowCard(
    state: CompanionState,
    controlsAllowed: Boolean,
    resultMessage: String?,
    onStartNext: () -> Unit,
    onConfirm: () -> Unit,
    onPause: () -> Unit,
    onResume: () -> Unit,
    onCancel: () -> Unit,
) {
    val flow = state.flow
    PanelCard {
        Text(flow.status.uppercase(), color = Cyan, style = MaterialTheme.typography.labelLarge)
        Text(flow.name, style = MaterialTheme.typography.headlineSmall)
        if (flow.totalSteps > 0) {
            LinearProgressIndicator(
                progress = { flow.completedSteps.toFloat() / flow.totalSteps },
                modifier = Modifier.fillMaxWidth().padding(vertical = 14.dp),
                color = Green,
                trackColor = PanelRaised,
            )
        }
        Surface(color = PanelRaised, shape = RoundedCornerShape(12.dp)) {
            Column(Modifier.fillMaxWidth().padding(18.dp)) {
                Text(flow.assignedRole.uppercase(), color = Amber)
                Text(flow.currentStep, style = MaterialTheme.typography.titleLarge)
                Text(flow.waitingFor, color = TextSecondary, modifier = Modifier.padding(top = 8.dp))
            }
        }
        Row(
            Modifier.fillMaxWidth().padding(top = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            if (flow.canStart) ActionButton("START NEXT", Modifier.weight(1f), controlsAllowed, onStartNext)
            if (flow.canConfirm) ActionButton("CONFIRM NOW", Modifier.weight(1f), controlsAllowed, onConfirm)
            if (flow.canPause) OutlinedButton(onClick = onPause, enabled = controlsAllowed) { Text("PAUSE") }
            if (flow.canResume) ActionButton("RESUME", Modifier.weight(1f), controlsAllowed, onResume)
            if (flow.canCancel) OutlinedButton(onClick = onCancel, enabled = controlsAllowed) { Text("CANCEL", color = Red) }
        }
        if (!controlsAllowed) {
            Text("View-only development pairing", color = Amber, modifier = Modifier.padding(top = 10.dp))
        }
        resultMessage?.let { Text(it, color = TextSecondary, modifier = Modifier.padding(top = 12.dp)) }
    }
}

@Composable
private fun FlowList(
    modifier: Modifier,
    flows: List<FlowListItem>,
    canStart: Boolean,
    onStartFlow: (String) -> Unit,
) {
    PanelCard(modifier) {
        Text("GATE TO GATE", style = MaterialTheme.typography.titleMedium)
        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            flows.forEach { flow ->
                val color = when (flow.state) {
                    "done" -> Green
                    "current" -> Cyan
                    "next" -> Amber
                    else -> TextSecondary
                }
                Surface(
                    color = if (flow.state == "current") PanelRaised else Color.Transparent,
                    shape = RoundedCornerShape(10.dp),
                    onClick = {
                        if (flow.state == "next" && canStart) onStartFlow(flow.id)
                    },
                ) {
                    Row(
                        Modifier.fillMaxWidth().padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        Text(if (flow.state == "done") "✓" else "•", color = color)
                        Column {
                            Text(flow.name, color = TextPrimary)
                            Text(flow.state.uppercase(), color = color, style = MaterialTheme.typography.labelSmall)
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun GsxCard(
    gsx: GsxState,
    controlsAllowed: Boolean,
    onOpen: () -> Unit,
    onChoice: (Int) -> Unit,
) {
    PanelCard {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Column {
                Text("GSX OPERATIONS", style = MaterialTheme.typography.titleMedium)
                Text(gsx.summary, color = TextSecondary)
            }
            if (gsx.canOpenMenu && gsx.choices.isEmpty()) {
                OutlinedButton(onClick = onOpen, enabled = controlsAllowed) { Text("OPEN MENU") }
            }
        }
        if (gsx.passengerPercent > 0) {
            LinearProgressIndicator(
                progress = { gsx.passengerPercent.coerceIn(0, 100) / 100f },
                modifier = Modifier.fillMaxWidth().padding(vertical = 12.dp),
            )
            Text(gsx.passengerProgress, color = TextSecondary)
        }
        gsx.promptTitle?.let { title ->
            HorizontalDivider(Modifier.padding(vertical = 12.dp), color = PanelRaised)
            Text(title, color = Amber, style = MaterialTheme.typography.titleMedium)
            gsx.choices.forEachIndexed { index, choice ->
                OutlinedButton(
                    onClick = { onChoice(index) },
                    enabled = controlsAllowed,
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                ) { Text(choice) }
            }
        }
    }
}

@Composable
private fun PanelCard(
    modifier: Modifier = Modifier,
    content: @Composable ColumnScope.() -> Unit,
) {
    Card(modifier, colors = CardDefaults.cardColors(containerColor = Panel)) {
        Column(
            Modifier.fillMaxWidth().padding(18.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp),
            content = content,
        )
    }
}

@Composable
private fun TelemetryValue(label: String, value: Int, unit: String) {
    Column(horizontalAlignment = Alignment.End) {
        Text(label, color = TextSecondary, style = MaterialTheme.typography.labelSmall)
        Text(value.toString(), style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
        Text(unit, color = Cyan, style = MaterialTheme.typography.labelSmall)
    }
}

@Composable
private fun ActionButton(
    label: String,
    modifier: Modifier,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    Button(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier.height(50.dp),
        colors = ButtonDefaults.buttonColors(containerColor = Cyan, contentColor = Night),
    ) { Text(label, fontWeight = FontWeight.Bold) }
}

@Composable
private fun ConnectionBadge(status: ConnectionStatus, message: String) {
    val color = when (status) {
        ConnectionStatus.CONNECTED_LOCAL, ConnectionStatus.CONNECTED_RELAY -> Green
        ConnectionStatus.CONNECTING -> Amber
        else -> Red
    }
    Surface(
        color = color.copy(alpha = 0.15f),
        shape = RoundedCornerShape(50),
        modifier = Modifier.padding(end = 16.dp),
    ) {
        Text(message, color = color, modifier = Modifier.padding(horizontal = 12.dp, vertical = 7.dp))
    }
}

@Composable
private fun VfoTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MaterialTheme.colorScheme.copy(
            primary = Cyan,
            secondary = Green,
            background = Night,
            surface = Panel,
            onBackground = TextPrimary,
            onSurface = TextPrimary,
        ),
        content = content,
    )
}
