// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.hazama.voicevoxruntime

import android.content.Context
import android.util.Log

object VoicevoxUnityBridge {

    private const val TAG = "VoicevoxUnityBridge"

    private var appContext: Context? = null

    @JvmStatic
    fun setContext(context: Context) {
        Log.d(TAG, "[setContext] enter")
        appContext = context.applicationContext
        Log.d(TAG, "[setContext] success package=${context.packageName}")
    }

    @JvmStatic
    fun initialize() {
        Log.d(TAG, "[initialize] enter")

        try {
            val context = appContext

            if (context == null) {
                Log.e(TAG, "[initialize] appContext is null")
                return
            }

            VoicevoxRuntimeManager.initialize(context)

            Log.d(TAG, "[initialize] success")
        } catch (e: Throwable) {
            Log.e(TAG, "[initialize] failed", e)
        }
    }

    @JvmStatic
    fun speak(
        text: String,
        modelFileName: String,
        styleId: Int
    ) {
        Log.d(TAG, "[speak] enter")
        Log.d(TAG, "[speak] text=$text")
        Log.d(TAG, "[speak] modelFileName=$modelFileName")
        Log.d(TAG, "[speak] styleId=$styleId")

        try {
            VoicevoxRuntimeManager.speak(
                text,
                modelFileName,
                styleId
            )

            Log.d(TAG, "[speak] success")
        } catch (e: Throwable) {
            Log.e(TAG, "[speak] failed", e)
        }
    }

    /**
     * Unity AudioSource再生用。
     *
     * C#側 VoicevoxAndroidBridge.SynthesizeToFile(text, styleId, fileName)
     * から呼ばれる想定。
     *
     * 既存 speak() は壊さず、こちらはWAVファイルを書き出して絶対パスを返す。
     */
    @JvmStatic
    fun synthesizeToFile(
        text: String,
        styleId: Int,
        fileName: String
    ): String {
        Log.d(TAG, "[synthesizeToFile] enter")
        Log.d(TAG, "[synthesizeToFile] text=$text")
        Log.d(TAG, "[synthesizeToFile] styleId=$styleId")
        Log.d(TAG, "[synthesizeToFile] fileName=$fileName")

        return try {
            val path = VoicevoxRuntimeManager.synthesizeToFile(
                text = text,
                modelFileName = "1.vvm",
                speakerId = styleId,
                fileName = fileName
            )

            Log.d(TAG, "[synthesizeToFile] result=$path")
            path
        } catch (e: Throwable) {
            Log.e(TAG, "[synthesizeToFile] failed", e)
            ""
        }
    }

    /**
     * 互換用。
     * C#側が別名探索しても拾えるように残す。
     */
    @JvmStatic
    fun synthesizeToWavFile(
        text: String,
        styleId: Int,
        fileName: String
    ): String {
        return synthesizeToFile(text, styleId, fileName)
    }

    /**
     * 互換用。
     */
    @JvmStatic
    fun speakToFile(
        text: String,
        styleId: Int,
        fileName: String
    ): String {
        return synthesizeToFile(text, styleId, fileName)
    }
}