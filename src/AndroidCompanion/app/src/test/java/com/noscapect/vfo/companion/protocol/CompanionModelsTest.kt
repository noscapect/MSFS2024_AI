package com.noscapect.vfo.companion.protocol

import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class CompanionModelsTest {
    private val json = Json { ignoreUnknownKeys = true }

    @Test
    fun commandUsesAllowListedWireName() {
        val encoded = json.encodeToString(
            CompanionCommand(
                requestId = "test-1",
                action = CompanionAction.START_NEXT_FLOW,
            )
        )

        assertTrue(encoded.contains("\"action\":\"start_next_flow\""))
    }

    @Test
    fun parsesStateWithUnknownAdditiveField() {
        val fixture = checkNotNull(javaClass.classLoader?.getResource("state.json"))
            .readText()
        val state = json.decodeFromString<CompanionState>(fixture)
        assertEquals("iniBuilds A321LR", state.aircraft.family)
        assertEquals(50, state.gsx.passengerPercent)
    }
}
