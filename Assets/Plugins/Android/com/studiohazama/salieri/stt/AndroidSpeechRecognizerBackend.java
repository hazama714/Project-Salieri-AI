// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;

import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;

/**
 * Preserved Android SpeechRecognizer backend.
 *
 * The product facade defaults to Vosk. This backend remains available for a
 * future explicit cloud/platform-recognizer route without changing Unity
 * callback names.
 */
final class AndroidSpeechRecognizerBackend {
    private static Activity activity;
    private static SpeechRecognizer recognizer;
    private static String unityObjectName;
    private static boolean initialized;

    private AndroidSpeechRecognizerBackend() {
    }

    static void initialize(String receiverName) {
        activity = UnityPlayer.currentActivity;
        unityObjectName = receiverName;
        if (activity == null) {
            send("OnSttError", "ANDROID_SPEECH_RECOGNIZER_NO_ACTIVITY");
            return;
        }

        activity.runOnUiThread(() -> {
            recreateRecognizer();
            initialized = recognizer != null;
        });
    }

    static void startListening() {
        if (activity == null) {
            return;
        }

        activity.runOnUiThread(() -> {
            if (!initialized || recognizer == null) {
                recreateRecognizer();
                initialized = recognizer != null;
            }
            if (recognizer == null) {
                send("OnSttError", "ANDROID_SPEECH_RECOGNIZER_UNAVAILABLE");
                return;
            }

            try {
                recognizer.cancel();
            } catch (RuntimeException ignored) {
            }

            Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
            intent.putExtra(
                    RecognizerIntent.EXTRA_LANGUAGE_MODEL,
                    RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
            intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, "ja-JP");
            intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_PREFERENCE, "ja-JP");
            intent.putExtra(
                    RecognizerIntent.EXTRA_ONLY_RETURN_LANGUAGE_PREFERENCE,
                    false);
            intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
            intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3);
            intent.putExtra(
                    RecognizerIntent.EXTRA_CALLING_PACKAGE,
                    activity.getPackageName());
            recognizer.startListening(intent);
        });
    }

    static void stopListening() {
        if (activity == null || recognizer == null) {
            return;
        }

        activity.runOnUiThread(() -> {
            try {
                recognizer.cancel();
            } catch (RuntimeException ignored) {
            }
        });
    }

    static void shutdown() {
        Activity currentActivity = activity;
        if (currentActivity == null) {
            clearState();
            return;
        }

        currentActivity.runOnUiThread(() -> {
            SpeechRecognizer ownedRecognizer = recognizer;
            recognizer = null;
            if (ownedRecognizer != null) {
                try {
                    ownedRecognizer.cancel();
                } catch (RuntimeException ignored) {
                }
                try {
                    ownedRecognizer.destroy();
                } catch (RuntimeException ignored) {
                }
            }
            clearState();
        });
    }

    static boolean isInitialized() {
        return initialized;
    }

    static boolean isReady() {
        return initialized && recognizer != null;
    }

    private static void recreateRecognizer() {
        SpeechRecognizer previous = recognizer;
        recognizer = null;
        if (previous != null) {
            try {
                previous.cancel();
            } catch (RuntimeException ignored) {
            }
            try {
                previous.destroy();
            } catch (RuntimeException ignored) {
            }
        }

        if (activity == null || !SpeechRecognizer.isRecognitionAvailable(activity)) {
            return;
        }

        recognizer = SpeechRecognizer.createSpeechRecognizer(activity);
        recognizer.setRecognitionListener(new RecognitionListener() {
            @Override
            public void onReadyForSpeech(Bundle params) {
                send("OnSttReady", "");
            }

            @Override
            public void onBeginningOfSpeech() {
                send("OnSttBegin", "");
            }

            @Override
            public void onEndOfSpeech() {
                send("OnSttEnd", "");
            }

            @Override
            public void onError(int error) {
                send("OnSttError", String.valueOf(error));
            }

            @Override
            public void onResults(Bundle results) {
                sendFirstResult(results, "OnSttResult", "NO_RESULT");
            }

            @Override
            public void onPartialResults(Bundle partialResults) {
                sendFirstResult(partialResults, "OnSttPartial", null);
            }

            @Override
            public void onRmsChanged(float rmsdB) {
            }

            @Override
            public void onBufferReceived(byte[] buffer) {
            }

            @Override
            public void onEvent(int eventType, Bundle params) {
            }
        });
    }

    private static void sendFirstResult(
            Bundle results,
            String callback,
            String emptyError) {
        ArrayList<String> matches =
                results != null
                        ? results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION)
                        : null;
        if (matches != null && !matches.isEmpty()) {
            send(callback, matches.get(0));
        } else if (emptyError != null) {
            send("OnSttError", emptyError);
        }
    }

    private static void send(String method, String value) {
        if (unityObjectName == null || unityObjectName.isEmpty()) {
            return;
        }
        UnityPlayer.UnitySendMessage(
                unityObjectName,
                method,
                value != null ? value : "");
    }

    private static void clearState() {
        recognizer = null;
        activity = null;
        unityObjectName = null;
        initialized = false;
    }
}
