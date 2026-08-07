package com.noscapect.vfo.companion

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import com.noscapect.vfo.companion.data.WebSocketCompanionRepository
import com.noscapect.vfo.companion.ui.CompanionApp

class MainActivity : ComponentActivity() {
    private val viewModel by lazy {
        ViewModelProvider(
            this,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    CompanionViewModel(
                        WebSocketCompanionRepository(applicationContext)
                    ) as T
            },
        )[CompanionViewModel::class.java]
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val pairingUri = intent?.data?.toString() ?: viewModel.savedPairingUri
        setContent {
            CompanionApp(viewModel = viewModel, initialPairingUri = pairingUri)
        }
    }
}
