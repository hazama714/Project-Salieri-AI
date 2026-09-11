// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt.vosk;

import android.app.Activity;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.io.IOException;

/**
 * Product bridge between Project Salieri's existing Unity callbacks and Vosk.
 *
 * Model readiness is intentionally kept inside Java. OnSttReady means that the
 * microphone session has actually started, not that the model finished loading.
 */
public final class SalieriVoskSttBridge implements VoskSttController.Listener {
    private static final String TAG = "SalieriVoskStt";
    private static final Object LOCK = new Object();

    private static SalieriVoskSttBridge instance;

    private final String unityObjectName;
    private VoskSttController controller;

    private SalieriVoskSttBridge(Activity activity, String receiverName) throws IOException {
        unityObjectName = receiverName;
        controller = new VoskSttController(activity.getApplicationContext(), this);
    }

    public static void initialize(String receiverName) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null || receiverName == null || receiverName.trim().isEmpty()) {
            Log.e(TAG, "initialize rejected: missing Activity or receiver name.");
            return;
        }

        synchronized (LOCK) {
            if (instance != null
                    && instance.controller != null
                    && !instance.controller.isShutdown()) {
                Log.i(TAG, "initialize ignored: bridge already exists.");
                return;
            }

            try {
                instance = new SalieriVoskSttBridge(activity, receiverName);
                instance.controller.initializeModel();
            } catch (IOException | RuntimeException ex) {
                Log.e(TAG, "initialize failed.", ex);
                UnityPlayer.UnitySendMessage(
                        receiverName,
                        "OnSttError",
                        "vosk_initialize_failed: " + safeMessage(ex));
                instance = null;
            }
        }
    }

    public static void startListening() {
        VoskSttController current = currentController();
        if (current == null) {
            sendCurrentError("vosk_not_initialized");
            return;
        }
        current.startListening();
    }

    /**
     * Project Salieri's existing Stop means cancel: no intermediate hypothesis
     * is promoted to a final user utterance.
     */
    public static void stopListening() {
        VoskSttController current = currentController();
        if (current != null) {
            current.stopListening();
        }
    }

    public static void shutdown() {
        VoskSttController current;
        synchronized (LOCK) {
            current = instance != null ? instance.controller : null;
        }
        if (current != null) {
            current.shutdown("unity_lifecycle");
        }
    }

    public static boolean isInitialized() {
        VoskSttController current = currentController();
        return current != null && current.isInitialized();
    }

    public static boolean isReady() {
        VoskSttController current = currentController();
        return current != null && current.isReady();
    }

    private static VoskSttController currentController() {
        synchronized (LOCK) {
            return instance != null ? instance.controller : null;
        }
    }

    private static void sendCurrentError(String error) {
        String receiver;
        synchronized (LOCK) {
            receiver = instance != null ? instance.unityObjectName : null;
        }
        if (receiver != null) {
            UnityPlayer.UnitySendMessage(receiver, "OnSttError", error);
        } else {
            Log.e(TAG, error);
        }
    }

    private void send(String method, String value) {
        UnityPlayer.UnitySendMessage(
                unityObjectName,
                method,
                value != null ? value : "");
    }

    @Override
    public void onSnapshot(VoskSttController.Snapshot snapshot) {
        if (snapshot != null && snapshot.state == VoskSttController.State.SHUTDOWN) {
            synchronized (LOCK) {
                if (instance == this) {
                    controller = null;
                    instance = null;
                }
            }
        }
    }

    @Override
    public void onReady() {
        send("OnSttReady", "");
    }

    @Override
    public void onBegin() {
        send("OnSttBegin", "");
    }

    @Override
    public void onPartial(String text) {
        send("OnSttPartial", text);
    }

    @Override
    public void onResult(String text) {
        send("OnSttResult", text);
    }

    @Override
    public void onEnd() {
        send("OnSttEnd", "");
    }

    @Override
    public void onError(String error) {
        send("OnSttError", error);
    }

    private static String safeMessage(Throwable error) {
        if (error == null || error.getMessage() == null) {
            return "unknown";
        }
        return error.getMessage();
    }
}
