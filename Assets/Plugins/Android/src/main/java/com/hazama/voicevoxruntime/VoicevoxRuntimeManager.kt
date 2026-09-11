// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.hazama.voicevoxruntime

import android.content.Context
import android.media.MediaPlayer
import android.util.Log
import java.io.File

object VoicevoxRuntimeManager {

    private const val TAG = "VoicevoxRuntimeManager"

    init {
        try {
            System.loadLibrary("c++_shared")
            Log.d(TAG, "[loadLibrary] c++_shared loaded")

            System.loadLibrary("onnxruntime")
            Log.d(TAG, "[loadLibrary] onnxruntime loaded")

            System.loadLibrary("voicevox_core")
            Log.d(TAG, "[loadLibrary] voicevox_core loaded")

            System.loadLibrary("voicevox_runtime")
            Log.d(TAG, "[loadLibrary] voicevox_runtime loaded")
        } catch (e: Throwable) {
            Log.e(TAG, "[loadLibrary] failed", e)
        }
    }

    private var appContext: Context? = null
    private var dictDir: File? = null
    private var modelDir: File? = null
    private var currentPlayer: MediaPlayer? = null
    private var isInitialized = false

    external fun synthesizeTest(
        dictPath: String,
        modelPath: String,
        text: String,
        speakerId: Int
    ): ByteArray

    fun initialize(context: Context) {
        Log.d(TAG, "[initialize] enter")

        if (isInitialized) {
            Log.d(TAG, "[initialize] Already initialized")
            return
        }

        try {
            appContext = context.applicationContext
            val ctx = appContext ?: run {
                Log.e(TAG, "[initialize] appContext is null")
                return
            }

            dictDir = File(ctx.filesDir, "open_jtalk_dic_utf_8-1.11")
            modelDir = File(ctx.filesDir, "voicevox/models")

            Log.d(TAG, "[initialize] filesDir=${ctx.filesDir.absolutePath}")
            Log.d(TAG, "[initialize] dictDir=${dictDir!!.absolutePath}")
            Log.d(TAG, "[initialize] modelDir=${modelDir!!.absolutePath}")

            if (!dictDir!!.exists()) {
                Log.d(TAG, "[initialize] copy dict start")
                copyAssetFolder(ctx, "VoiceVox/open_jtalk_dic_utf_8-1.11", dictDir!!)
                Log.d(TAG, "[initialize] copy dict end exists=${dictDir!!.exists()}")
            } else {
                Log.d(TAG, "[initialize] dict already exists")
            }

            if (!modelDir!!.exists()) {
                Log.d(TAG, "[initialize] copy models start")
                copyAssetFolder(ctx, "VoiceVox/models", modelDir!!)
                Log.d(TAG, "[initialize] copy models end exists=${modelDir!!.exists()}")
            } else {
                Log.d(TAG, "[initialize] models already exists")
            }

            isInitialized = true
            Log.d(TAG, "[initialize] success")
        } catch (e: Throwable) {
            Log.e(TAG, "[initialize] failed", e)
        }
    }

    fun speak(text: String, modelFileName: String, speakerId: Int) {
        Log.d(TAG, "[speak] enter text=$text modelFileName=$modelFileName speakerId=$speakerId")

        try {
            val wavBytes = synthesizeToBytes(
                text = text,
                modelFileName = modelFileName,
                speakerId = speakerId,
                callerTag = "speak"
            ) ?: return

            val ctx = appContext ?: run {
                Log.e(TAG, "[speak] appContext is null")
                return
            }

            playWavBytes(ctx, wavBytes)
        } catch (e: Throwable) {
            Log.e(TAG, "[speak] failed", e)
        }
    }

    /**
     * Unity AudioSource再生用。
     *
     * 既存の speak() は Android MediaPlayer で直接再生する。
     * こちらは再生せず、WAVを書き出して絶対パスを返す。
     */
    fun synthesizeToFile(
        text: String,
        modelFileName: String,
        speakerId: Int,
        fileName: String
    ): String {
        Log.d(
            TAG,
            "[synthesizeToFile] enter text=$text modelFileName=$modelFileName speakerId=$speakerId fileName=$fileName"
        )

        try {
            val ctx = appContext ?: run {
                Log.e(TAG, "[synthesizeToFile] appContext is null")
                return ""
            }

            val wavBytes = synthesizeToBytes(
                text = text,
                modelFileName = modelFileName,
                speakerId = speakerId,
                callerTag = "synthesizeToFile"
            ) ?: return ""

            val safeFileName = sanitizeWavFileName(fileName)
            val wavFile = File(ctx.cacheDir, safeFileName)

            val wavWriteStartedNs = System.nanoTime()
            wavFile.writeBytes(wavBytes)
            val wavWriteDurationMs =
                (System.nanoTime() - wavWriteStartedNs) / 1_000_000.0

            Log.d(
                TAG,
                "[synthesizeToFile] success path=${wavFile.absolutePath} " +
                    "size=${wavFile.length()} wavWriteMs=$wavWriteDurationMs"
            )

            return wavFile.absolutePath
        } catch (e: Throwable) {
            Log.e(TAG, "[synthesizeToFile] failed", e)
            return ""
        }
    }

    private fun synthesizeToBytes(
        text: String,
        modelFileName: String,
        speakerId: Int,
        callerTag: String
    ): ByteArray? {
        if (!isInitialized) {
            Log.e(TAG, "[$callerTag] Not initialized")
            return null
        }

        val dict = dictDir ?: run {
            Log.e(TAG, "[$callerTag] dictDir is null")
            return null
        }

        val models = modelDir ?: run {
            Log.e(TAG, "[$callerTag] modelDir is null")
            return null
        }

        val modelFile = File(models, modelFileName)

        Log.d(TAG, "[$callerTag] dictPath=${dict.absolutePath} exists=${dict.exists()}")
        Log.d(
            TAG,
            "[$callerTag] modelFile=${modelFile.absolutePath} exists=${modelFile.exists()} size=${if (modelFile.exists()) modelFile.length() else -1}"
        )

        if (!modelFile.exists()) {
            Log.e(TAG, "[$callerTag] Model not found: ${modelFile.absolutePath}")
            return null
        }

        Log.d(TAG, "[$callerTag] before synthesizeTest")

        val synthesisStartedNs = System.nanoTime()
        val wavBytes = synthesizeTest(
            dict.absolutePath,
            modelFile.absolutePath,
            text,
            speakerId
        )
        val synthesisDurationMs =
            (System.nanoTime() - synthesisStartedNs) / 1_000_000.0

        Log.d(
            TAG,
            "[$callerTag] after synthesizeTest wavSize=${wavBytes.size} " +
                "nativeSynthesisMs=$synthesisDurationMs"
        )

        if (wavBytes.isEmpty()) {
            Log.e(TAG, "[$callerTag] wavBytes empty")
            return null
        }

        return wavBytes
    }

    private fun sanitizeWavFileName(fileName: String): String {
        val baseName =
            if (fileName.isBlank()) {
                "voicevox_last.wav"
            } else {
                File(fileName).name
            }

        return if (baseName.endsWith(".wav", ignoreCase = true)) {
            baseName
        } else {
            "$baseName.wav"
        }
    }

    private fun playWavBytes(context: Context, wavBytes: ByteArray) {
        Log.d(TAG, "[playWavBytes] enter wavSize=${wavBytes.size}")

        try {
            stopCurrentPlayer()

            val wavFile = File(context.cacheDir, "voicevox_output.wav")
            wavFile.writeBytes(wavBytes)

            Log.d(TAG, "[playWavBytes] wavFile=${wavFile.absolutePath} size=${wavFile.length()}")

            val player = MediaPlayer()
            currentPlayer = player

            player.setDataSource(wavFile.absolutePath)
            player.prepare()
            player.start()

            Log.d(TAG, "[playWavBytes] start")

            player.setOnCompletionListener {
                Log.d(TAG, "[playWavBytes] completed")
                it.release()
                if (currentPlayer == it) currentPlayer = null
            }
        } catch (e: Throwable) {
            Log.e(TAG, "[playWavBytes] failed", e)
        }
    }

    fun stopCurrentPlayer() {
        currentPlayer?.let {
            try {
                if (it.isPlaying) it.stop()
            } catch (e: Throwable) {
                Log.e(TAG, "[stopCurrentPlayer] stop failed", e)
            }

            try {
                it.release()
            } catch (e: Throwable) {
                Log.e(TAG, "[stopCurrentPlayer] release failed", e)
            }
        }

        currentPlayer = null
    }

    private fun copyAssetFolder(context: Context, assetPath: String, outDir: File) {
        Log.d(TAG, "[copyAssetFolder] assetPath=$assetPath outDir=${outDir.absolutePath}")

        outDir.mkdirs()

        val files = context.assets.list(assetPath)

        if (files == null) {
            Log.e(TAG, "[copyAssetFolder] files is null: $assetPath")
            return
        }

        for (name in files) {
            val childAssetPath = "$assetPath/$name"
            val outFile = File(outDir, name)
            val childFiles = context.assets.list(childAssetPath)

            if (childFiles != null && childFiles.isNotEmpty()) {
                copyAssetFolder(context, childAssetPath, outFile)
            } else {
                context.assets.open(childAssetPath).use { input ->
                    outFile.outputStream().use { output ->
                        input.copyTo(output)
                    }
                }

                Log.d(TAG, "[copyAssetFolder] copied $childAssetPath size=${outFile.length()}")
            }
        }
    }
}
