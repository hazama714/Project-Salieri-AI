// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt;

import com.studiohazama.salieri.stt.vosk.SalieriVoskSttBridge;

/**
 * Stable facade used by the existing C# AndroidSTTService.
 *
 * The public class name and lowercase methods are retained so MainScene and
 * the C# bridge require no new serialized reference or backend-selection UI.
 */
public final class SalieriSpeechRecognizerBridge {
    private enum Backend {
        VOSK,
        ANDROID_SPEECH_RECOGNIZER
    }

    private static Backend activeBackend = Backend.VOSK;

    private SalieriSpeechRecognizerBridge() {
    }

    public static void init(String receiverName) {
        activeBackend = Backend.VOSK;
        SalieriVoskSttBridge.initialize(receiverName);
    }

    public static void initAndroidSpeechRecognizer(String receiverName) {
        activeBackend = Backend.ANDROID_SPEECH_RECOGNIZER;
        AndroidSpeechRecognizerBackend.initialize(receiverName);
    }

    public static void startListening() {
        if (activeBackend == Backend.VOSK) {
            SalieriVoskSttBridge.startListening();
        } else {
            AndroidSpeechRecognizerBackend.startListening();
        }
    }

    public static void stopListening() {
        if (activeBackend == Backend.VOSK) {
            SalieriVoskSttBridge.stopListening();
        } else {
            AndroidSpeechRecognizerBackend.stopListening();
        }
    }

    public static void shutdown() {
        if (activeBackend == Backend.VOSK) {
            SalieriVoskSttBridge.shutdown();
        } else {
            AndroidSpeechRecognizerBackend.shutdown();
        }
    }

    public static void destroy() {
        SalieriVoskSttBridge.shutdown();
    }

    public static boolean isInitialized() {
        return activeBackend == Backend.VOSK
                ? SalieriVoskSttBridge.isInitialized()
                : AndroidSpeechRecognizerBackend.isInitialized();
    }

    public static boolean isReady() {
        return activeBackend == Backend.VOSK
                ? SalieriVoskSttBridge.isReady()
                : AndroidSpeechRecognizerBackend.isReady();
    }
}
