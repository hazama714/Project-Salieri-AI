// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

package com.studiohazama.salieri.stt.vosk;

import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;

import org.json.JSONException;
import org.json.JSONObject;
import org.vosk.Model;
import org.vosk.Recognizer;
import org.vosk.android.RecognitionListener;
import org.vosk.android.SpeechService;

import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;

final class VoskSttController {
    static final String PROVIDER_ID = "vosk_android_0.3.75";
    static final String RUNTIME_VERSION = "0.3.75";
    static final String MODEL_ID = "vosk-model-small-ja-0.22";
    static final String MODEL_VERSION = "0.22";
    static final String MODEL_SHA256 =
            "EFA092D280153A77615E9E0C7D7283E93E600DE3D19D3BEC686C57EF19D52EAC";

    private static final float SAMPLE_RATE = 16000.0f;
    private static final long NO_SESSION = -1L;
    private static final AtomicLong SESSION_SEQUENCE = new AtomicLong(0L);
    private static final AtomicLong GENERATION_SEQUENCE = new AtomicLong(0L);
    private static final String MODEL_MANIFEST_ASSET =
            "Vosk/models/vosk-model-small-ja-0.22.manifest.json";

    enum State {
        UNINITIALIZED,
        INITIALIZING,
        READY,
        LISTENING,
        STOPPING,
        SHUTTING_DOWN,
        SHUTDOWN,
        ERROR
    }

    interface Listener {
        void onSnapshot(Snapshot snapshot);

        void onReady();

        void onBegin();

        void onPartial(String text);

        void onResult(String text);

        void onEnd();

        void onError(String error);
    }

    private final Object resourceLock = new Object();
    private final Context appContext;
    private final Listener listener;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService controlExecutor =
            Executors.newSingleThreadExecutor(runnable -> {
                Thread thread = new Thread(runnable, "VoskGateA-Control");
                thread.setDaemon(true);
                return thread;
            });
    private final AtomicLong lifecycleGeneration =
            new AtomicLong(GENERATION_SEQUENCE.incrementAndGet());
    private final AtomicBoolean shutdownRequested = new AtomicBoolean(false);
    private final BundledModelInstaller modelInstaller;
    private final JsonlLogWriter logWriter;

    private volatile State state = State.UNINITIALIZED;
    private volatile long activeSessionId = NO_SESSION;
    private volatile boolean airplaneModeManual;

    private Model model;
    private Recognizer recognizer;
    private SpeechService speechService;

    private String partialText = "";
    private String finalText = "";
    private String errorText = "";
    private String listeningStartedAt = "";
    private String firstPartialAt = "";
    private String finalAt = "";
    private long listeningStartedElapsedMs = -1L;
    private long firstPartialElapsedMs = -1L;
    private long finalElapsedMs = -1L;
    private long archiveVerifyMs;
    private long extractionMs;
    private long nativeModelInitMs;
    private long totalModelLoadMs;
    private boolean modelWasExtracted;
    private boolean pendingStart;
    private boolean finalSent;
    private boolean beginSent;
    private boolean endSent;
    private boolean cancelRequested;
    private MemorySampler.Snapshot memorySnapshot =
            MemorySampler.capture("controller_created");

    VoskSttController(Context context, Listener listener) throws IOException {
        this.appContext = context.getApplicationContext();
        this.listener = listener;
        this.modelInstaller = new BundledModelInstaller(appContext, MODEL_MANIFEST_ASSET);
        this.logWriter = new JsonlLogWriter(appContext);
        writeEvent("controller_created", NO_SESSION, null, null);
        publishSnapshot();
    }

    boolean isShutdown() {
        return state == State.SHUTDOWN;
    }

    boolean isInitialized() {
        synchronized (resourceLock) {
            return model != null
                    && state != State.SHUTTING_DOWN
                    && state != State.SHUTDOWN;
        }
    }

    boolean isReady() {
        return state == State.READY;
    }

    void setAirplaneModeManual(boolean value) {
        airplaneModeManual = value;
        writeEvent("airplane_mode_manual_changed", activeSessionId, null, null);
        publishSnapshot();
    }

    void reportPermissionDenied() {
        errorText = "permission_denied: RECORD_AUDIO was not granted.";
        writeError("permission_denied", activeSessionId, null);
        publishSnapshot();
    }

    void initializeModel() {
        final long generation;
        synchronized (resourceLock) {
            if (shutdownRequested.get()
                    || (state != State.UNINITIALIZED && state != State.ERROR)
                    || model != null) {
                reportInvalidState("Initialize requires UNINITIALIZED with no model.");
                return;
            }
            state = State.INITIALIZING;
            generation = lifecycleGeneration.get();
            errorText = "";
            memorySnapshot = MemorySampler.capture("before_model_load");
        }
        writeEvent("model_initialization_started", NO_SESSION, memorySnapshot, null);
        publishSnapshot();

        executeControl(() -> {
            long totalStart = SystemClock.elapsedRealtime();
            BundledModelInstaller.Result installed;
            Model createdModel = null;
            try {
                installed = modelInstaller.installAndVerify();
                long nativeStart = SystemClock.elapsedRealtime();
                createdModel = new Model(installed.modelDirectory.getAbsolutePath());
                long nativeMs = SystemClock.elapsedRealtime() - nativeStart;

                boolean shouldStart;
                synchronized (resourceLock) {
                    if (shutdownRequested.get()
                            || generation != lifecycleGeneration.get()
                            || state != State.INITIALIZING) {
                        closeModelQuietly(createdModel);
                        writeEvent("model_initialization_discarded", NO_SESSION, null, null);
                        return;
                    }

                    model = createdModel;
                    createdModel = null;
                    archiveVerifyMs = installed.archiveVerifyMs;
                    extractionMs = installed.extractionMs;
                    nativeModelInitMs = nativeMs;
                    totalModelLoadMs = SystemClock.elapsedRealtime() - totalStart;
                    modelWasExtracted = installed.extracted;
                    state = State.READY;
                    memorySnapshot = MemorySampler.capture("after_model_load");
                    shouldStart = pendingStart;
                    pendingStart = false;
                }

                JSONObject details = commonDetails(NO_SESSION);
                try {
                    details.put("model_path", installed.modelDirectory.getAbsolutePath());
                    details.put("model_extracted", installed.extracted);
                    details.put("archive_verify_ms", installed.archiveVerifyMs);
                    details.put("extraction_ms", installed.extractionMs);
                    details.put("native_model_init_ms", nativeMs);
                    details.put("total_model_load_ms", totalModelLoadMs);
                    mergeMemory(details, memorySnapshot);
                } catch (JSONException ignored) {
                    // Diagnostic fields must not invalidate a successful model initialization.
                }
                logWriter.write("model_initialization_completed", details);
                publishSnapshot();
                if (shouldStart) {
                    writeEvent("pending_start_released", NO_SESSION, null, null);
                    startListening();
                }
            } catch (BundledModelInstaller.ModelInstallException ex) {
                closeModelQuietly(createdModel);
                failCurrentOperation(ex.errorCode, ex);
            } catch (IOException ex) {
                closeModelQuietly(createdModel);
                failCurrentOperation("native_model_init_failed", ex);
            } catch (RuntimeException ex) {
                closeModelQuietly(createdModel);
                failCurrentOperation("native_model_init_failed", ex);
            }
        });
    }

    void startListening() {
        final long sessionId;
        final long generation;
        final Model currentModel;
        synchronized (resourceLock) {
            if (shutdownRequested.get()) {
                reportInvalidState("Start rejected after shutdown request.");
                return;
            }
            if (state == State.INITIALIZING) {
                if (!pendingStart) {
                    pendingStart = true;
                    writeEvent("start_deferred_until_model_ready", NO_SESSION, null, null);
                } else {
                    writeEvent("duplicate_deferred_start_ignored", NO_SESSION, null, null);
                }
                return;
            }
            if (state != State.READY || model == null) {
                reportInvalidState("Start requires READY with an initialized model.");
                return;
            }

            sessionId = SESSION_SEQUENCE.incrementAndGet();
            activeSessionId = sessionId;
            generation = lifecycleGeneration.get();
            currentModel = model;
            state = State.LISTENING;
            partialText = "";
            finalText = "";
            errorText = "";
            listeningStartedAt = isoUtcNow();
            firstPartialAt = "";
            finalAt = "";
            listeningStartedElapsedMs = SystemClock.elapsedRealtime();
            firstPartialElapsedMs = -1L;
            finalElapsedMs = -1L;
            finalSent = false;
            beginSent = false;
            endSent = false;
            cancelRequested = false;
            memorySnapshot = MemorySampler.capture("listening_start_requested");
        }
        writeEvent("listening_start_requested", sessionId, memorySnapshot, null);
        publishSnapshot();

        executeControl(() -> createAndStartSession(sessionId, generation, currentModel));
    }

    void stopListening() {
        synchronized (resourceLock) {
            if (state == State.INITIALIZING && pendingStart) {
                pendingStart = false;
                writeEvent("pending_start_cancelled", NO_SESSION, null, null);
                return;
            }
            if (shutdownRequested.get()
                    || (state != State.LISTENING && state != State.STOPPING)) {
                reportInvalidState("Stop requires LISTENING or STOPPING.");
                return;
            }
        }
        cancelListening();
    }

    void cancelListening() {
        final long cancelledSession;
        final long generation;
        synchronized (resourceLock) {
            if (state == State.STOPPING && cancelRequested) {
                writeEvent("duplicate_cancel_ignored", activeSessionId, null, null);
                return;
            }
            if (shutdownRequested.get()
                    || (state != State.LISTENING && state != State.STOPPING)) {
                reportInvalidState("Cancel requires LISTENING or STOPPING.");
                return;
            }
            cancelledSession = activeSessionId;
            activeSessionId = NO_SESSION;
            state = State.STOPPING;
            generation = lifecycleGeneration.get();
            cancelRequested = true;
        }
        writeError("cancelled", cancelledSession, null);
        publishSnapshot();

        executeControl(() -> cancelSession(cancelledSession, generation));
    }

    void shutdown(String reason) {
        if (!shutdownRequested.compareAndSet(false, true)) {
            return;
        }

        final long generation = GENERATION_SEQUENCE.incrementAndGet();
        lifecycleGeneration.set(generation);
        final long invalidatedSession;
        synchronized (resourceLock) {
            invalidatedSession = activeSessionId;
            activeSessionId = NO_SESSION;
            state = State.SHUTTING_DOWN;
            pendingStart = false;
            cancelRequested = true;
        }
        JSONObject reasonDetails = commonDetails(invalidatedSession);
        try {
            reasonDetails.put("shutdown_reason", reason);
            reasonDetails.put("lifecycle_generation", generation);
        } catch (JSONException ignored) {
        }
        logWriter.write("shutdown_requested", reasonDetails);
        publishSnapshot();

        try {
            controlExecutor.execute(() -> performShutdown(reason, invalidatedSession));
        } catch (RejectedExecutionException ex) {
            synchronized (resourceLock) {
                state = State.SHUTDOWN;
            }
            publishSnapshot();
            logWriter.close();
        }
    }

    private void createAndStartSession(long sessionId, long generation, Model currentModel) {
        Recognizer createdRecognizer = null;
        SpeechService createdService = null;
        try {
            try {
                createdRecognizer = new Recognizer(currentModel, SAMPLE_RATE);
            } catch (IOException ex) {
                failSession("recognizer_init_failed", sessionId, generation, ex);
                return;
            }

            try {
                createdService = new SpeechService(createdRecognizer, SAMPLE_RATE);
            } catch (IOException ex) {
                closeSessionResources(null, createdRecognizer, false);
                createdRecognizer = null;
                failSession("audio_record_init_failed", sessionId, generation, ex);
                return;
            }

            synchronized (resourceLock) {
                if (!isSessionGenerationCurrent(sessionId, generation)
                        || (state != State.LISTENING && state != State.STOPPING)) {
                    closeSessionResources(createdService, createdRecognizer, false);
                    writeDiscardedCallback("start_discarded", sessionId, generation);
                    return;
                }
                recognizer = createdRecognizer;
                speechService = createdService;
                createdRecognizer = null;
                createdService = null;
            }

            boolean started;
            synchronized (resourceLock) {
                if (!isSessionGenerationCurrent(sessionId, generation)
                        || (state != State.LISTENING && state != State.STOPPING)
                        || speechService == null) {
                    SessionResources abandoned = detachSessionResources();
                    closeSessionResources(
                            abandoned.service,
                            abandoned.recognizer,
                            false);
                    writeDiscardedCallback("start_discarded", sessionId, generation);
                    return;
                }
                started = speechService.startListening(
                        new SessionRecognitionListener(sessionId, generation));
            }
            if (!started) {
                throw new IllegalStateException("SpeechService rejected startListening.");
            }
            writeEvent("listening_started", sessionId, MemorySampler.capture("listening_start"), null);
            mainHandler.post(listener::onReady);
            publishSnapshot();
        } catch (SecurityException ex) {
            closeSessionResources(createdService, createdRecognizer, false);
            failSession("permission_denied", sessionId, generation, ex);
        } catch (RuntimeException ex) {
            closeSessionResources(createdService, createdRecognizer, false);
            String code = ex.getMessage() != null
                    && ex.getMessage().toLowerCase(Locale.US).contains("audio")
                    ? "audio_record_init_failed"
                    : "listening_start_failed";
            failSession(code, sessionId, generation, ex);
        }
    }

    private void stopSessionAndRequestFinal(long sessionId, long generation) {
        SessionResources resources = detachSessionResources();
        try {
            if (resources.service != null) {
                resources.service.stop();
            }
        } catch (RuntimeException ex) {
            closeSessionResources(resources.service, resources.recognizer, false);
            failSession("speech_service_error", sessionId, generation, ex);
            return;
        }

        closeSessionResources(resources.service, resources.recognizer, false);

        // SpeechService posts onFinalResult before stop() returns. This post is enqueued
        // after it, so the accepted final callback is processed before the session closes.
        mainHandler.post(() -> {
            synchronized (resourceLock) {
                if (!isSessionGenerationCurrent(sessionId, generation)
                        || state != State.STOPPING
                        || shutdownRequested.get()) {
                    return;
                }
                activeSessionId = NO_SESSION;
                state = State.READY;
                memorySnapshot = MemorySampler.capture("after_stop");
            }
            writeEvent("stop_completed", sessionId, memorySnapshot, null);
            publishSnapshot();
        });
    }

    private void cancelSession(long sessionId, long generation) {
        SessionResources resources = detachSessionResources();
        try {
            if (resources.service != null) {
                resources.service.cancel();
            }
        } catch (RuntimeException ex) {
            writeError("speech_service_error", sessionId, ex);
        } finally {
            closeSessionResources(resources.service, resources.recognizer, false);
        }

        mainHandler.post(() -> {
            synchronized (resourceLock) {
                if (generation != lifecycleGeneration.get() || shutdownRequested.get()) {
                    return;
                }
                if (state == State.STOPPING) {
                    state = State.READY;
                    memorySnapshot = MemorySampler.capture("after_cancel");
                }
            }
            writeEvent("cancel_completed", sessionId, memorySnapshot, null);
            sendEndOnce();
            publishSnapshot();
        });
    }

    private void finishAcceptedFinal(long sessionId, long generation) {
        SessionResources resources = detachSessionResources();
        try {
            if (resources.service != null) {
                resources.service.cancel();
            }
        } catch (RuntimeException ex) {
            writeError("speech_service_error_after_final", sessionId, ex);
        } finally {
            closeSessionResources(resources.service, resources.recognizer, false);
        }

        mainHandler.post(() -> {
            synchronized (resourceLock) {
                if (generation != lifecycleGeneration.get() || shutdownRequested.get()) {
                    return;
                }
                state = State.READY;
                memorySnapshot = MemorySampler.capture("after_final");
            }
            writeEvent("final_session_completed", sessionId, memorySnapshot, null);
            sendEndOnce();
            publishSnapshot();
        });
    }

    private void sendEndOnce() {
        synchronized (resourceLock) {
            if (endSent) {
                return;
            }
            endSent = true;
        }
        listener.onEnd();
    }

    private void failSession(
            String errorCode,
            long sessionId,
            long generation,
            Throwable error) {
        String callbackError;
        synchronized (resourceLock) {
            if (!isSessionGenerationCurrent(sessionId, generation)) {
                writeDiscardedCallback("session_failure_discarded", sessionId, generation);
                return;
            }
            activeSessionId = NO_SESSION;
            state = model != null ? State.READY : State.ERROR;
            errorText = formatError(errorCode, error);
            callbackError = errorText;
        }

        SessionResources resources = detachSessionResources();
        executeControl(
                () -> closeSessionResources(resources.service, resources.recognizer, true));
        writeError(errorCode, sessionId, error);
        mainHandler.post(() -> {
            listener.onError(callbackError);
            sendEndOnce();
        });
        publishSnapshot();
    }

    private void failCurrentOperation(String errorCode, Throwable error) {
        String callbackError;
        synchronized (resourceLock) {
            if (shutdownRequested.get()) {
                return;
            }
            state = State.ERROR;
            errorText = formatError(errorCode, error);
            pendingStart = false;
            callbackError = errorText;
        }
        writeError(errorCode, activeSessionId, error);
        mainHandler.post(() -> listener.onError(callbackError));
        publishSnapshot();
    }

    private void performShutdown(String reason, long invalidatedSession) {
        SessionResources resources = detachSessionResources();
        Model ownedModel;
        synchronized (resourceLock) {
            ownedModel = model;
            model = null;
        }

        try {
            if (resources.service != null) {
                try {
                    resources.service.cancel();
                } catch (RuntimeException ex) {
                    writeError("speech_service_error", invalidatedSession, ex);
                }
            }
            closeSessionResources(resources.service, resources.recognizer, false);
            closeModelQuietly(ownedModel);
        } finally {
            synchronized (resourceLock) {
                state = State.SHUTDOWN;
                memorySnapshot = MemorySampler.capture("after_shutdown");
            }
            JSONObject details = commonDetails(invalidatedSession);
            try {
                details.put("shutdown_reason", reason);
                mergeMemory(details, memorySnapshot);
            } catch (JSONException ignored) {
            }
            logWriter.write("shutdown_completed", details);
            publishSnapshot();
            logWriter.close();
            controlExecutor.shutdown();
        }
    }

    private SessionResources detachSessionResources() {
        synchronized (resourceLock) {
            SessionResources resources = new SessionResources(speechService, recognizer);
            speechService = null;
            recognizer = null;
            return resources;
        }
    }

    private void closeSessionResources(
            SpeechService service,
            Recognizer ownedRecognizer,
            boolean cancelFirst) {
        if (service != null) {
            if (cancelFirst) {
                try {
                    service.cancel();
                } catch (RuntimeException ex) {
                    writeError("speech_service_error", activeSessionId, ex);
                }
            }
            try {
                service.shutdown();
            } catch (RuntimeException ex) {
                writeError("speech_service_error", activeSessionId, ex);
            }
        }
        if (ownedRecognizer != null) {
            try {
                ownedRecognizer.close();
            } catch (RuntimeException ex) {
                writeError("unknown", activeSessionId, ex);
            }
        }
    }

    private static void closeModelQuietly(Model ownedModel) {
        if (ownedModel == null) {
            return;
        }
        try {
            ownedModel.close();
        } catch (RuntimeException ignored) {
        }
    }

    private boolean isSessionGenerationCurrent(long sessionId, long generation) {
        return sessionId != NO_SESSION
                && sessionId == activeSessionId
                && generation == lifecycleGeneration.get()
                && !shutdownRequested.get();
    }

    private boolean shouldAcceptCallback(long sessionId, long generation) {
        State current = state;
        return isSessionGenerationCurrent(sessionId, generation)
                && (current == State.LISTENING || current == State.STOPPING);
    }

    private void handlePartial(long sessionId, long generation, String hypothesis) {
        if (!shouldAcceptCallback(sessionId, generation)) {
            writeDiscardedCallback("partial_discarded", sessionId, generation);
            return;
        }
        String text = parseVoskText(hypothesis, "partial");
        boolean sendBegin = false;
        synchronized (resourceLock) {
            if (!shouldAcceptCallback(sessionId, generation)) {
                writeDiscardedCallback("partial_discarded", sessionId, generation);
                return;
            }
            partialText = text;
            if (!text.isEmpty() && firstPartialElapsedMs < 0L) {
                firstPartialElapsedMs = SystemClock.elapsedRealtime();
                firstPartialAt = isoUtcNow();
                memorySnapshot = MemorySampler.capture("first_partial");
            }
            if (!text.isEmpty() && !beginSent) {
                beginSent = true;
                sendBegin = true;
            }
        }
        JSONObject details = callbackDetails(sessionId, text, "");
        writeEvent("partial_result", sessionId, memorySnapshot, details);
        if (sendBegin) {
            mainHandler.post(listener::onBegin);
        }
        if (!text.isEmpty()) {
            mainHandler.post(() -> listener.onPartial(text));
        }
        publishSnapshot();
    }

    private void handleUtteranceResult(long sessionId, long generation, String hypothesis) {
        if (!shouldAcceptCallback(sessionId, generation)) {
            writeDiscardedCallback("utterance_result_discarded", sessionId, generation);
            return;
        }
        String text = parseVoskText(hypothesis, "text");
        if (text.isEmpty()) {
            writeEvent("empty_utterance_result_ignored", sessionId, null, null);
            return;
        }
        boolean sendBegin = false;
        synchronized (resourceLock) {
            if (!shouldAcceptCallback(sessionId, generation)) {
                writeDiscardedCallback("utterance_result_discarded", sessionId, generation);
                return;
            }
            if (finalSent) {
                writeDiscardedCallback("duplicate_utterance_result_discarded", sessionId, generation);
                return;
            }
            finalSent = true;
            if (!beginSent) {
                beginSent = true;
                sendBegin = true;
            }
            finalText = text;
            partialText = "";
            finalElapsedMs = SystemClock.elapsedRealtime();
            finalAt = isoUtcNow();
            memorySnapshot = MemorySampler.capture("final");
            activeSessionId = NO_SESSION;
            state = State.STOPPING;
        }
        writeEvent("final_result", sessionId, memorySnapshot, callbackDetails(sessionId, "", text));
        if (sendBegin) {
            mainHandler.post(listener::onBegin);
        }
        mainHandler.post(() -> listener.onResult(text));
        publishSnapshot();
        executeControl(() -> finishAcceptedFinal(sessionId, generation));
    }

    private void handleFinal(long sessionId, long generation, String hypothesis) {
        writeDiscardedCallback("speech_service_final_callback_ignored", sessionId, generation);
    }

    private void handleSpeechServiceError(long sessionId, long generation, Exception exception) {
        if (!shouldAcceptCallback(sessionId, generation)) {
            writeDiscardedCallback("error_callback_discarded", sessionId, generation);
            return;
        }
        failSession("speech_service_error", sessionId, generation, exception);
    }

    private void handleTimeout(long sessionId, long generation) {
        if (!shouldAcceptCallback(sessionId, generation)) {
            writeDiscardedCallback("timeout_discarded", sessionId, generation);
            return;
        }
        writeEvent("recognition_timeout", sessionId, null, null);
        publishSnapshot();
    }

    private JSONObject callbackDetails(long sessionId, String partial, String complete) {
        JSONObject details = commonDetails(sessionId);
        try {
            details.put("partial_text", partial);
            details.put("final_text", complete);
            details.put("start_at", listeningStartedAt);
            details.put("first_partial_at", firstPartialAt);
            details.put("final_at", finalAt);
            details.put(
                    "start_to_first_partial_ms",
                    firstPartialElapsedMs >= 0L
                            ? firstPartialElapsedMs - listeningStartedElapsedMs
                            : JSONObject.NULL);
            details.put(
                    "start_to_final_ms",
                    finalElapsedMs >= 0L
                            ? finalElapsedMs - listeningStartedElapsedMs
                            : JSONObject.NULL);
        } catch (JSONException ignored) {
        }
        return details;
    }

    private void reportInvalidState(String message) {
        errorText = "invalid_state: " + message;
        writeError("invalid_state", activeSessionId, new IllegalStateException(message));
        publishSnapshot();
    }

    private void writeDiscardedCallback(String eventType, long sessionId, long generation) {
        JSONObject details = commonDetails(sessionId);
        try {
            details.put("callback_lifecycle_generation", generation);
            details.put("current_lifecycle_generation", lifecycleGeneration.get());
            details.put("current_active_session_id", activeSessionId);
        } catch (JSONException ignored) {
        }
        logWriter.write(eventType, details);
    }

    private void writeError(String code, long sessionId, Throwable error) {
        JSONObject details = commonDetails(sessionId);
        try {
            details.put("error_code", code);
            details.put(
                    "exception_class",
                    error != null ? error.getClass().getName() : JSONObject.NULL);
            details.put(
                    "exception_message",
                    error != null && error.getMessage() != null
                            ? error.getMessage()
                            : JSONObject.NULL);
        } catch (JSONException ignored) {
        }
        logWriter.write("error", details);
    }

    private void writeEvent(
            String eventType,
            long sessionId,
            MemorySampler.Snapshot memory,
            JSONObject extra) {
        JSONObject details = commonDetails(sessionId);
        try {
            if (memory != null) {
                mergeMemory(details, memory);
            }
            if (extra != null) {
                java.util.Iterator<String> keys = extra.keys();
                while (keys.hasNext()) {
                    String key = keys.next();
                    details.put(key, extra.get(key));
                }
            }
        } catch (JSONException ignored) {
        }
        logWriter.write(eventType, details);
    }

    private JSONObject commonDetails(long sessionId) {
        JSONObject details = new JSONObject();
        try {
            details.put(
                    "listen_session_id",
                    sessionId == NO_SESSION ? JSONObject.NULL : sessionId);
            details.put("provider_id", PROVIDER_ID);
            details.put("runtime_version", RUNTIME_VERSION);
            details.put("model_id", MODEL_ID);
            details.put("model_version", MODEL_VERSION);
            details.put("model_archive_sha256", MODEL_SHA256);
            details.put("airplane_mode_manual", airplaneModeManual);
            details.put("controller_state", state.name());
            details.put("lifecycle_generation", lifecycleGeneration.get());
        } catch (JSONException ignored) {
        }
        return details;
    }

    private static void mergeMemory(JSONObject target, MemorySampler.Snapshot memory)
            throws JSONException {
        JSONObject memoryJson = memory.toJson();
        java.util.Iterator<String> keys = memoryJson.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            target.put(key, memoryJson.get(key));
        }
    }

    private void executeControl(Runnable operation) {
        try {
            controlExecutor.execute(operation);
        } catch (RejectedExecutionException ex) {
            failCurrentOperation("shutdown", ex);
        }
    }

    private void publishSnapshot() {
        Snapshot snapshot;
        synchronized (resourceLock) {
            snapshot = new Snapshot(
                    state,
                    activeSessionId,
                    lifecycleGeneration.get(),
                    partialText,
                    finalText,
                    errorText,
                    listeningStartedAt,
                    firstPartialAt,
                    finalAt,
                    elapsedLatency(firstPartialElapsedMs),
                    elapsedLatency(finalElapsedMs),
                    archiveVerifyMs,
                    extractionMs,
                    nativeModelInitMs,
                    totalModelLoadMs,
                    modelWasExtracted,
                    model != null,
                    memorySnapshot,
                    logWriter.getLogFile().getAbsolutePath(),
                    airplaneModeManual);
        }
        mainHandler.post(() -> listener.onSnapshot(snapshot));
    }

    private long elapsedLatency(long eventElapsedMs) {
        return eventElapsedMs >= 0L && listeningStartedElapsedMs >= 0L
                ? eventElapsedMs - listeningStartedElapsedMs
                : -1L;
    }

    private static String parseVoskText(String json, String key) {
        if (json == null || json.isEmpty()) {
            return "";
        }
        try {
            return new JSONObject(json).optString(key, "").trim();
        } catch (JSONException ignored) {
            return "";
        }
    }

    private static String formatError(String code, Throwable error) {
        String message = error != null ? error.getMessage() : null;
        return code + (message == null || message.isEmpty() ? "" : ": " + message);
    }

    private static String isoUtcNow() {
        SimpleDateFormat formatter =
                new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US);
        formatter.setTimeZone(TimeZone.getTimeZone("UTC"));
        return formatter.format(new Date());
    }

    private final class SessionRecognitionListener implements RecognitionListener {
        private final long sessionId;
        private final long generation;

        SessionRecognitionListener(long sessionId, long generation) {
            this.sessionId = sessionId;
            this.generation = generation;
        }

        @Override
        public void onPartialResult(String hypothesis) {
            handlePartial(sessionId, generation, hypothesis);
        }

        @Override
        public void onResult(String hypothesis) {
            handleUtteranceResult(sessionId, generation, hypothesis);
        }

        @Override
        public void onFinalResult(String hypothesis) {
            handleFinal(sessionId, generation, hypothesis);
        }

        @Override
        public void onError(Exception exception) {
            handleSpeechServiceError(sessionId, generation, exception);
        }

        @Override
        public void onTimeout() {
            handleTimeout(sessionId, generation);
        }
    }

    private static final class SessionResources {
        final SpeechService service;
        final Recognizer recognizer;

        SessionResources(SpeechService service, Recognizer recognizer) {
            this.service = service;
            this.recognizer = recognizer;
        }
    }

    static final class Snapshot {
        final State state;
        final long sessionId;
        final long lifecycleGeneration;
        final String partialText;
        final String finalText;
        final String errorText;
        final String listeningStartedAt;
        final String firstPartialAt;
        final String finalAt;
        final long firstPartialLatencyMs;
        final long finalLatencyMs;
        final long archiveVerifyMs;
        final long extractionMs;
        final long nativeModelInitMs;
        final long totalModelLoadMs;
        final boolean modelWasExtracted;
        final boolean modelInitialized;
        final MemorySampler.Snapshot memory;
        final String logPath;
        final boolean airplaneModeManual;

        Snapshot(
                State state,
                long sessionId,
                long lifecycleGeneration,
                String partialText,
                String finalText,
                String errorText,
                String listeningStartedAt,
                String firstPartialAt,
                String finalAt,
                long firstPartialLatencyMs,
                long finalLatencyMs,
                long archiveVerifyMs,
                long extractionMs,
                long nativeModelInitMs,
                long totalModelLoadMs,
                boolean modelWasExtracted,
                boolean modelInitialized,
                MemorySampler.Snapshot memory,
                String logPath,
                boolean airplaneModeManual) {
            this.state = state;
            this.sessionId = sessionId;
            this.lifecycleGeneration = lifecycleGeneration;
            this.partialText = partialText;
            this.finalText = finalText;
            this.errorText = errorText;
            this.listeningStartedAt = listeningStartedAt;
            this.firstPartialAt = firstPartialAt;
            this.finalAt = finalAt;
            this.firstPartialLatencyMs = firstPartialLatencyMs;
            this.finalLatencyMs = finalLatencyMs;
            this.archiveVerifyMs = archiveVerifyMs;
            this.extractionMs = extractionMs;
            this.nativeModelInitMs = nativeModelInitMs;
            this.totalModelLoadMs = totalModelLoadMs;
            this.modelWasExtracted = modelWasExtracted;
            this.modelInitialized = modelInitialized;
            this.memory = memory;
            this.logPath = logPath;
            this.airplaneModeManual = airplaneModeManual;
        }
    }
}
