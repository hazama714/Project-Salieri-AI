// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// RuntimeConnectionSettings support

using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SalieriAI.Core.Limbo;
using SalieriAI.Core.Reflex.Cognitive;
using SalieriAI.Core.State;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Expression.Voice.Synthesis;
using SalieriAI.Runtime;
using SalieriAI.Core.Diagnostics.Performance;

public sealed class VoicePlaybackController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private VoicevoxAndroidBridge voicevoxBridge;
    [SerializeField] private WindowsVoicevoxSynthesisProvider windowsVoicevoxProvider;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private LimboPermission limboPermission;
    [SerializeField] private InteractionStateController stateController;

    [Header("Runtime")]
    [SerializeField] private RuntimeConnectionSettings runtimeSettings;
    [SerializeField] private RuntimeInteractionSettings interactionSettings;

    [Header("VOICEVOX Settings")]
    [SerializeField] private int styleId = 14;
    [SerializeField] private string outputFileName = "voicevox_last.wav";

    [Header("Debug")]
    [SerializeField] private bool subscribeOnEnable = true;
    [SerializeField] private bool logVerbose = true;

    [Header("Debug Test")]
    [SerializeField] private bool playOnStartForDebug = false;

    [SerializeField]
    [TextArea]
    private string debugText = "・ｽ・ｽ・ｽ・ｽﾉゑｿｽ・ｽﾍ。";

    private Coroutine playbackCoroutine;
    private int playbackCoroutineGeneration = -1;

    private CancellationTokenSource requestCancellation;
    private int requestCancellationGeneration = -1;
    private Task<string> windowsSynthesisTask;
    private int windowsSynthesisTaskGeneration = -1;
    private string windowsWavPath;
    private int windowsWavOwnerGeneration = -1;

    private AudioClip ownedAudioClip;
    private int audioClipOwnerGeneration = -1;

    private int requestGeneration;
    private int speakingOwnerGeneration = -1;
    private InteractionState stateBeforeSpeaking = InteractionState.Idle;

    private string runtimeSpeechId = string.Empty;
    private string runtimeResponseCorrelationId = string.Empty;
    private int runtimeSpeechGeneration = -1;
    private bool runtimeSpeechTerminal;

#if UNITY_ANDROID
    private TTSSynthesisProductionVoiceRuntime androidTtsRuntime;
    private TTSSynthesisAndroidVoicevoxBackendFactory androidTtsBackendFactory;
    private string androidWaveId = string.Empty;
    private string androidRequestId = string.Empty;
    private int androidSynthesisGeneration = -1;
    private TTSSynthesisWaveResult androidSynthesisTerminal;
    private string androidWavPath;
    private int androidWavOwnerGeneration = -1;
    private bool androidShutdownRequested;
#endif

    public event Action<SpeechPlaybackRuntimeFact> SpeechLifecycleObserved;

    public string CurrentRuntimeSpeechId => runtimeSpeechId;
    public bool HasActiveRuntimeSpeech =>
        runtimeSpeechGeneration == requestGeneration &&
        !runtimeSpeechTerminal &&
        !string.IsNullOrEmpty(runtimeSpeechId);

    private void Awake()
    {
        if (voicevoxBridge == null)
            voicevoxBridge = FindObjectOfType<VoicevoxAndroidBridge>();

        if (windowsVoicevoxProvider == null)
            windowsVoicevoxProvider = GetComponent<WindowsVoicevoxSynthesisProvider>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (limboPermission == null)
            limboPermission = FindObjectOfType<LimboPermission>();

        if (stateController == null)
            stateController = FindObjectOfType<InteractionStateController>();

        if (runtimeSettings == null)
            runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();

        if (interactionSettings == null)
            interactionSettings = FindObjectOfType<RuntimeInteractionSettings>();

#if UNITY_ANDROID
        InitializeAndroidTtsRuntime();
#endif

        if (logVerbose)
        {
            Debug.Log(
                $"[VoicePlaybackController][Awake] " +
                $"androidBridge={(voicevoxBridge != null ? voicevoxBridge.name : "null")} " +
                $"windowsProvider={(windowsVoicevoxProvider != null ? windowsVoicevoxProvider.name : "null")} " +
                $"audioSource={(audioSource != null ? audioSource.name : "null")} " +
                $"limbo={(limboPermission != null ? limboPermission.name : "null")} " +
                $"state={(stateController != null ? stateController.name : "null")} " +
                $"runtime={(runtimeSettings != null ? runtimeSettings.name : "null")} " +
                $"interaction={(interactionSettings != null ? interactionSettings.name : "null")} " +
                $"outputFileName={outputFileName}"
            );
        }
    }

    private void Start()
    {
        if (playOnStartForDebug)
            PlayText(debugText);
    }

    private void Update()
    {
#if UNITY_ANDROID
        PumpAndroidTtsCompletion();
#endif
    }

    private void OnEnable()
    {
        if (!subscribeOnEnable)
            return;

        ResponseBus.OnResponseEnvelope += OnResponseEnvelope;

        if (logVerbose)
            Debug.Log("[VoicePlaybackController] subscribed ResponseBus.OnResponseEnvelope");
    }

    private void OnDisable()
    {
        if (subscribeOnEnable)
        {
            ResponseBus.OnResponseEnvelope -= OnResponseEnvelope;

            if (logVerbose)
                Debug.Log("[VoicePlaybackController] unsubscribed ResponseBus.OnResponseEnvelope");
        }

        CancelCurrentRequestAndInvalidate("component-disabled");
    }

    private void OnDestroy()
    {
        CancelCurrentRequestAndInvalidate("component-destroyed");

#if UNITY_ANDROID
        RequestAndroidTtsShutdown("component-destroyed");
        PumpAndroidTtsCompletion();
        TryDisposeAndroidTtsRuntime();
#endif
    }

    private void OnApplicationQuit()
    {
#if UNITY_ANDROID
        RequestAndroidTtsShutdown("application-quit");
#endif
    }

    private void OnResponseEnvelope(ResponseEnvelope envelope)
    {
        if (envelope == null)
            return;
        PlayTextInternal(envelope.Text, envelope);
    }

    public void PlayText(string text)
    {
        PlayTextInternal(text, null);
    }

    private void PlayTextInternal(string text, ResponseEnvelope envelope)
    {
        if (!IsVoiceEnabled())
        {
            Debug.Log("[VoicePlaybackController] Voice disabled by RuntimeConnectionSettings.");
            FailCorrelatedResponse(envelope, "VOICE_DISABLED");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[VoicePlaybackController] text is empty");
            FailCorrelatedResponse(envelope, "VOICE_TEXT_EMPTY");
            return;
        }

        if (IsEmergencyBlocked())
        {
            Debug.Log("[VoicePlaybackController] Speak blocked by Emergency Limbo.");
            FailCorrelatedResponse(envelope, "VOICE_BLOCKED_BY_EMERGENCY");
            return;
        }

        CancelCurrentRequestAndInvalidate("replaced-by-new-request");

        int generation = requestGeneration;
        requestCancellation = new CancellationTokenSource();
        requestCancellationGeneration = generation;
        playbackCoroutineGeneration = generation;
        BeginRuntimeSpeechLifecycle(generation, envelope);

        try
        {
            playbackCoroutine = StartCoroutine(
                PlayTextRoutine(
                    text.Trim(),
                    generation,
                    requestCancellation.Token
                )
            );
        }
        catch (Exception exception)
        {
            FailCurrentRequest(
                generation, null, false, false,
                "PLAYBACK_COROUTINE_START_FAILED:" +
                exception.GetType().Name);
        }
    }

    private static void FailCorrelatedResponse(
        ResponseEnvelope envelope,
        string reason)
    {
        if (envelope == null || !envelope.HasConversationCorrelation)
            return;
        ConversationTurnLifecycleRuntime.FailTurn(
            envelope.TurnId,
            reason);
    }

    private IEnumerator PlayTextRoutine(
        string text,
        int generation,
        CancellationToken cancellationToken
    )
    {
        if (!IsCurrentGeneration(generation))
            yield break;

        if (!IsVoiceEnabled())
        {
            LogForCurrent(
                generation,
                "Voice disabled at routine start."
            );
            InterruptCurrentRequest(
                generation, null, false, false,
                "VOICE_DISABLED_AT_ROUTINE_START");
            yield break;
        }

        if (!HasSynthesisProviderForCurrentPlatform(generation))
        {
            FailCurrentRequest(
                generation, null, false, false,
                "SYNTHESIS_PROVIDER_UNAVAILABLE");
            yield break;
        }

        if (audioSource == null)
        {
            LogErrorForCurrent(generation, "audioSource is null");
            FailCurrentRequest(
                generation, null, false, false,
                "AUDIO_SOURCE_UNAVAILABLE");
            yield break;
        }

        if (IsEmergencyBlocked())
        {
            LogForCurrent(
                generation,
                "Speak blocked by Emergency Limbo at routine start."
            );
            InterruptCurrentRequest(
                generation, null, false, false,
                "EMERGENCY_LIMBO_BLOCKED");
            yield break;
        }

        EnterSpeakingState(generation);

        /*
         * Conversation responses may return while the runtime is Listening.
         * Listening intentionally has CanSpeak=false. Move to Speaking first,
         * then allow LimboPermissionResolver one frame to apply its profile.
         */
        yield return null;

        if (!IsCurrentGeneration(generation))
            yield break;

        if (!CanSpeakNow())
        {
            LogForCurrent(
                generation,
                "Speak blocked by Limbo after Speaking transition."
            );
            InterruptCurrentRequest(
                generation, null, true, false,
                "LIMBO_PERMISSION_DENIED");
            yield break;
        }

        if (logVerbose && IsCurrentGeneration(generation))
            Debug.Log($"[VoicePlaybackController] Synthesize: {text}");

        string wavPath = null;
        bool windowsGeneratedWav = false;
        bool androidGeneratedWav = false;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        windowsGeneratedWav = true;
        Task<string> synthesisTask;

        try
        {
            synthesisTask = windowsVoicevoxProvider.SynthesizeToFileAsync(
                text,
                styleId,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            LogErrorForCurrent(
                generation,
                "Windows synthesis start failed: " + exception
            );
            FailCurrentRequest(
                generation, null, true, false,
                "WINDOWS_SYNTHESIS_START_FAILED:" +
                exception.GetType().Name);
            yield break;
        }

        if (!IsCurrentGeneration(generation))
        {
            ObserveAbandonedWindowsTask(synthesisTask, generation);
            yield break;
        }

        windowsSynthesisTask = synthesisTask;
        windowsSynthesisTaskGeneration = generation;

        while (!synthesisTask.IsCompleted)
        {
            if (!IsCurrentGeneration(generation))
                yield break;

            yield return null;
        }

        if (!IsCurrentGeneration(generation))
            yield break;

        if (synthesisTask.IsCanceled)
        {
            LogForCurrent(generation, "Windows synthesis was cancelled.");
            ClearWindowsTaskReference(generation);
            InterruptCurrentRequest(
                generation, null, true, false,
                "WINDOWS_SYNTHESIS_CANCELLED");
            yield break;
        }

        if (synthesisTask.IsFaulted)
        {
            Exception exception = synthesisTask.Exception != null
                ? synthesisTask.Exception.GetBaseException()
                : new InvalidOperationException("Windows synthesis failed.");
            string failedWavPath = null;

            WindowsVoicevoxSynthesisProvider.TryGetGeneratedOutputPath(
                exception,
                out failedWavPath
            );

            LogErrorForCurrent(
                generation,
                "Windows synthesis failed: " + exception
            );
            ClearWindowsTaskReference(generation);
            FailCurrentRequest(
                generation,
                failedWavPath,
                true,
                true,
                "WINDOWS_SYNTHESIS_FAILED:" +
                exception.GetType().Name
            );
            yield break;
        }

        wavPath = synthesisTask.Result;
        ClearWindowsTaskReference(generation);

        if (IsCurrentGeneration(generation))
        {
            windowsWavPath = wavPath;
            windowsWavOwnerGeneration = generation;
        }
#elif UNITY_ANDROID
        if (androidTtsRuntime == null)
        {
            FailCurrentRequest(
                generation, null, true, false,
                "ANDROID_TTS_RUNTIME_UNAVAILABLE");
            yield break;
        }

        string requestId = runtimeSpeechId;
        string waveId =
            "tts-production:" + GetInstanceID() + ":" + generation + ":"
            + Guid.NewGuid().ToString("N");
        string correlatedOutputFileName =
            BuildCorrelatedAndroidOutputFileName(outputFileName, waveId);
        DateTime requestedAtUtc = DateTime.UtcNow;
        TTSSynthesisWaveRequest waveRequest =
            new TTSSynthesisWaveRequest(
                waveId,
                requestId,
                text,
                styleId,
                correlatedOutputFileName,
                requestedAtUtc,
                null,
                null,
                false);

        TTSSynthesisAdmissionResult admissionResult;
        try
        {
            // Presentation replacement and synthesis admission are separate
            // concerns. Once admitted, a native call drains to return even if
            // this presentation generation is later replaced.
            admissionResult = androidTtsRuntime.Submit(
                waveRequest,
                DateTime.UtcNow,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            FailCurrentRequest(
                generation, null, true, false,
                "ANDROID_TTS_SUBMIT_FAILED:" +
                exception.GetType().Name);
            yield break;
        }

        if (admissionResult != TTSSynthesisAdmissionResult.AcceptedActive
            && admissionResult
                != TTSSynthesisAdmissionResult.AcceptedPending)
        {
            LogErrorForCurrent(
                generation,
                "AR1-B synthesis admission rejected. result="
                + admissionResult + " waveId=" + waveId
                + " requestId=" + requestId);
            FailCurrentRequest(
                generation, null, true, false,
                "ANDROID_TTS_ADMISSION_" + admissionResult);
            yield break;
        }

        androidWaveId = waveId;
        androidRequestId = requestId;
        androidSynthesisGeneration = generation;
        androidSynthesisTerminal = null;

        Debug.Log(
            "[AR1B-W8-Production][Submit] generation=" + generation
            + " waveId=" + waveId
            + " requestId=" + requestId
            + " styleId=" + styleId
            + " outputFileName=" + correlatedOutputFileName
            + " admission=" + admissionResult
            + " mainThread=" + Thread.CurrentThread.ManagedThreadId);

        while (androidSynthesisTerminal == null)
        {
            if (!IsCurrentGeneration(generation))
                yield break;

            yield return null;
        }

        if (!IsCurrentGeneration(generation))
            yield break;

        TTSSynthesisWaveResult androidTerminal = androidSynthesisTerminal;
        if (androidTerminal.TerminalState
            != TTSSynthesisWaveState.Succeeded
            || !androidTerminal.IsAudioReady)
        {
            string failure =
                "ANDROID_TTS_" + androidTerminal.TerminalState + ":"
                + androidTerminal.NativeErrorCode;
            LogErrorForCurrent(
                generation,
                "AR1-B synthesis failed. " + failure);
            FailCurrentRequest(
                generation, null, true, false, failure);
            yield break;
        }

        wavPath = androidTerminal.AudioArtifact.WavFilePath;
        androidGeneratedWav = true;
#else
        LogErrorForCurrent(
            generation,
            "VOICEVOX synthesis is not supported on this platform."
        );
        FailCurrentRequest(
            generation, null, true, false,
            "SYNTHESIS_PLATFORM_UNSUPPORTED");
        yield break;
#endif

        bool deleteGeneratedWavAfterUse =
            windowsGeneratedWav || androidGeneratedWav;

        if (!IsCurrentGeneration(generation))
        {
            DeleteStaleGeneratedWav(
                wavPath,
                generation,
                deleteGeneratedWavAfterUse);
            yield break;
        }

        if (string.IsNullOrEmpty(wavPath))
        {
            LogErrorForCurrent(
                generation,
                "SynthesizeToFile returned empty path"
            );
            FailCurrentRequest(
                generation, wavPath, true,
                deleteGeneratedWavAfterUse,
                "SYNTHESIS_OUTPUT_PATH_EMPTY");
            yield break;
        }

        if (!File.Exists(wavPath))
        {
            LogErrorForCurrent(
                generation,
                "wav not found: " + wavPath
            );
            FailCurrentRequest(
                generation, wavPath, true,
                deleteGeneratedWavAfterUse,
                "SYNTHESIS_OUTPUT_NOT_FOUND");
            yield break;
        }

        // WAV loading is itself a generation-owned side effect.
        if (!IsCurrentGeneration(generation))
        {
            DeleteStaleGeneratedWav(
                wavPath,
                generation,
                deleteGeneratedWavAfterUse);
            yield break;
        }

        string url = "file://" + wavPath;

        bool playbackInterrupted = false;
        long wavReadStarted =
            SalieriRuntimePerformanceProbe.Timestamp();

        using (UnityWebRequest request =
               UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            long wavByteLength = 0L;
            try
            {
                wavByteLength = new FileInfo(wavPath).Length;
            }
            catch
            {
                // Measurement must not change playback failure semantics.
            }
            SalieriRuntimePerformanceProbe.RecordDuration(
                SalieriRuntimePerformanceMetric.TtsWavRead,
                wavReadStarted,
                wavByteLength);

            if (!IsCurrentGeneration(generation))
                yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogErrorForCurrent(
                    generation,
                    "Load wav failed: " + request.error
                );
                FailCurrentRequest(
                    generation,
                    wavPath,
                    true,
                    deleteGeneratedWavAfterUse,
                    "WAV_LOAD_FAILED:" + request.error
                );
                yield break;
            }

            if (!IsVoiceEnabled())
            {
                LogForCurrent(
                    generation,
                    "Voice disabled before playback."
                );
                InterruptCurrentRequest(
                    generation,
                    wavPath,
                    true,
                    deleteGeneratedWavAfterUse,
                    "VOICE_DISABLED_BEFORE_PLAYBACK"
                );
                yield break;
            }

            long audioClipStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            SalieriRuntimePerformanceProbe.RecordDuration(
                SalieriRuntimePerformanceMetric.AudioClipCreate,
                audioClipStarted,
                wavByteLength);

            if (clip == null)
            {
                LogErrorForCurrent(
                    generation,
                    "Load wav succeeded but AudioClip is null."
                );
                FailCurrentRequest(
                    generation,
                    wavPath,
                    true,
                    deleteGeneratedWavAfterUse,
                    "AUDIO_CLIP_NULL"
                );
                yield break;
            }

            if (!IsCurrentGeneration(generation))
            {
                if (clip != null)
                    Destroy(clip);

                yield break;
            }

            ownedAudioClip = clip;
            audioClipOwnerGeneration = generation;

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();

            if (!audioSource.isPlaying)
            {
                LogErrorForCurrent(
                    generation,
                    "AudioSource.Play did not start playback."
                );
                FailCurrentRequest(
                    generation, wavPath, true, deleteGeneratedWavAfterUse,
                    "AUDIO_PLAYBACK_DID_NOT_START");
                yield break;
            }

            EmitRuntimeSpeechLifecycle(
                generation,
                SpeechAdapterLifecycle.PlaybackStarted,
                string.Empty);

            if (logVerbose && IsCurrentGeneration(generation))
            {
                Debug.Log(
                    $"[VoicePlaybackController] AudioSource.Play " +
                    $"path:{wavPath} length:{clip.length:F2}"
                );
            }

            while (IsCurrentGeneration(generation) &&
                   audioSource != null &&
                   audioSource.isPlaying)
            {
                if (!IsVoiceEnabled())
                {
                    audioSource.Stop();
                    LogForCurrent(
                        generation,
                        "Playback stopped by RuntimeConnectionSettings."
                    );
                    playbackInterrupted = true;
                    break;
                }

                yield return null;
            }
        }

        if (!IsCurrentGeneration(generation))
            yield break;

        if (audioSource == null)
        {
            FailCurrentRequest(
                generation, wavPath, true, deleteGeneratedWavAfterUse,
                "AUDIO_SOURCE_LOST_DURING_PLAYBACK");
            yield break;
        }

        if (playbackInterrupted)
        {
            InterruptCurrentRequest(
                generation, wavPath, true, deleteGeneratedWavAfterUse,
                "VOICE_DISABLED_DURING_PLAYBACK");
            yield break;
        }

        CompleteCurrentRequest(
            generation, wavPath, true, deleteGeneratedWavAfterUse);
    }

#if UNITY_ANDROID
    private void InitializeAndroidTtsRuntime()
    {
        if (androidTtsRuntime != null || voicevoxBridge == null)
            return;

        androidTtsBackendFactory =
            new TTSSynthesisAndroidVoicevoxBackendFactory(voicevoxBridge);
        androidTtsRuntime = new TTSSynthesisProductionVoiceRuntime(
            androidTtsBackendFactory,
            () => DateTime.UtcNow,
            Thread.CurrentThread.ManagedThreadId);
        androidTtsRuntime.ArtifactHandedOff +=
            OnAndroidTtsArtifactHandedOff;
        androidTtsRuntime.ShutdownArtifactCleanupRequired +=
            OnAndroidTtsShutdownArtifactCleanupRequired;

        Debug.Log(
            "[AR1B-W8-Production][Owner] orchestratorInstances=1 "
            + "activeLimit=1 pendingLimit=1 policy=BoundedFifo "
            + "overflow=RejectIncoming designatedMainThread="
            + Thread.CurrentThread.ManagedThreadId);
    }

    private void PumpAndroidTtsCompletion()
    {
        if (androidTtsRuntime == null)
            return;

        Task<TTSSynthesisBackgroundWorkResult> completion =
            androidTtsRuntime.ActiveCompletionTask;
        if (completion == null || !completion.IsCompleted)
            return;

        DateTime nowUtc = DateTime.UtcNow;
        TTSSynthesisOrchestrationCycleResult cycle;
        try
        {
            cycle = androidTtsRuntime.PumpCompletedActive(nowUtc, nowUtc);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[AR1B-W8-Production][Pump] exception=" + exception);
            return;
        }

        if (!cycle.Completed)
        {
            Debug.LogError(
                "[AR1B-W8-Production][Pump] rejected disposition="
                + cycle.Disposition + " diagnostic=" + cycle.Diagnostic);
            return;
        }

        TTSSynthesisCompletionDispatchResult terminal = cycle.Terminal;
        TTSSynthesisWaveResult result =
            terminal == null ? null : terminal.WaveResult;
        if (result != null && IsCurrentAndroidSynthesis(
                result.WaveId,
                result.RequestId))
        {
            androidSynthesisTerminal = result;
        }

        if (terminal != null)
        {
            Debug.Log(
                "[AR1B-W8-Production][Completion] waveId="
                + cycle.WaveId + " requestId=" + cycle.RequestId
                + " terminal=" + terminal.FinalState
                + " terminalCount="
                + androidTtsRuntime.Orchestrator.TerminalCount
                + " handoffCount="
                + androidTtsRuntime.Orchestrator.ArtifactHandoffCount
                + " releaseCount="
                + androidTtsRuntime.Orchestrator.ReleaseCount
                + " nativeCalls="
                + androidTtsBackendFactory.Metrics.CallCount
                + " maxConcurrent="
                + androidTtsBackendFactory.Metrics.MaximumConcurrentCount
                + " workerThread=" + terminal.WorkerThreadId
                + " backendThread=" + terminal.BackendCallThreadId
                + " completionThread="
                + terminal.CompletionCallerThreadId
                + " designatedMainThread="
                + terminal.DesignatedMainThreadId
                + " jniThread="
                + voicevoxBridge.LastBackgroundJniThreadId
                + " attach=" + voicevoxBridge.BackgroundAttachCount
                + " detach=" + voicevoxBridge.BackgroundDetachCount
                + " promoted=" + cycle.PromotedWorkerStarted);
        }

        if (androidShutdownRequested)
            TryDisposeAndroidTtsRuntime();
    }

    private void OnAndroidTtsArtifactHandedOff(
        string waveId,
        string requestId,
        TTSSynthesisAudioArtifact artifact)
    {
        if (artifact == null || !artifact.IsUsable)
            return;

        if (IsCurrentAndroidSynthesis(waveId, requestId))
        {
            androidWavPath = artifact.WavFilePath;
            androidWavOwnerGeneration = androidSynthesisGeneration;
            Debug.Log(
                "[AR1B-W8-Production][ArtifactHandoff] waveId="
                + waveId + " requestId=" + requestId
                + " path=" + artifact.WavFilePath
                + " bytes=" + artifact.ByteLength
                + " handoffThread="
                + Thread.CurrentThread.ManagedThreadId);
            return;
        }

        TryDeleteGeneratedWav(
            artifact.WavFilePath,
            -1,
            "stale-android-artifact");
        Debug.Log(
            "[AR1B-W8-Production][ArtifactStale] waveId=" + waveId
            + " requestId=" + requestId + " deleted=true");
    }

    private void OnAndroidTtsShutdownArtifactCleanupRequired(
        string waveId,
        string requestId,
        TTSSynthesisAudioArtifact artifact)
    {
        if (artifact != null)
        {
            TryDeleteGeneratedWav(
                artifact.WavFilePath,
                -1,
                "shutdown-android-artifact");
        }

        Debug.Log(
            "[AR1B-W8-Production][ShutdownArtifactCleanup] waveId="
            + waveId + " requestId=" + requestId
            + " artifactPresent=" + (artifact != null));
    }

    private bool IsCurrentAndroidSynthesis(
        string waveId,
        string requestId)
    {
        return androidSynthesisGeneration == requestGeneration
            && string.Equals(
                androidWaveId,
                waveId,
                StringComparison.Ordinal)
            && string.Equals(
                androidRequestId,
                requestId,
                StringComparison.Ordinal);
    }

    private void ClearAndroidSynthesisPresentation(int generation)
    {
        if (androidSynthesisGeneration != generation)
            return;

        androidWaveId = string.Empty;
        androidRequestId = string.Empty;
        androidSynthesisGeneration = -1;
        androidSynthesisTerminal = null;

        if (androidWavOwnerGeneration == generation)
        {
            androidWavPath = null;
            androidWavOwnerGeneration = -1;
        }
    }

    private void RequestAndroidTtsShutdown(string reason)
    {
        if (androidTtsRuntime == null || androidShutdownRequested)
            return;

        androidShutdownRequested = true;
        TTSSynthesisShutdownRequestResult result =
            androidTtsRuntime.RequestShutdown(DateTime.UtcNow);
        Debug.Log(
            "[AR1B-W8-Production][Shutdown] reason=" + reason
            + " disposition=" + result.Disposition
            + " lifecycle=" + result.Lifecycle
            + " admissionClosed=" + result.AdmissionClosed
            + " pendingCancelled="
            + result.PendingCancelledAndReleased
            + " activeDrainRequired=" + result.ActiveDrainRequired);
    }

    private void TryDisposeAndroidTtsRuntime()
    {
        if (androidTtsRuntime == null)
            return;

        bool disposed = androidTtsRuntime.TryDispose();
        if (disposed)
        {
            androidTtsRuntime.ArtifactHandedOff -=
                OnAndroidTtsArtifactHandedOff;
            androidTtsRuntime.ShutdownArtifactCleanupRequired -=
                OnAndroidTtsShutdownArtifactCleanupRequired;
            Debug.Log(
                "[AR1B-W8-Production][Shutdown] lifecycle=Disposed");
        }
    }

    private static string BuildCorrelatedAndroidOutputFileName(
        string configuredFileName,
        string waveId)
    {
        string baseName = Path.GetFileNameWithoutExtension(
            string.IsNullOrWhiteSpace(configuredFileName)
                ? "voicevox_last.wav"
                : configuredFileName);
        string extension = Path.GetExtension(configuredFileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".wav";

        char[] chars = waveId.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return baseName + "_" + new string(chars) + extension;
    }
#endif

    private bool HasSynthesisProviderForCurrentPlatform(int generation)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (windowsVoicevoxProvider != null &&
            windowsVoicevoxProvider.isActiveAndEnabled &&
            windowsVoicevoxProvider.IsSupported)
        {
            return true;
        }

        LogErrorForCurrent(
            generation,
            "WindowsVoicevoxSynthesisProvider is missing or disabled."
        );
        return false;
#elif UNITY_ANDROID
        if (voicevoxBridge != null
            && voicevoxBridge.IsBackgroundSynthesisReady
            && androidTtsRuntime != null)
            return true;

        LogErrorForCurrent(
            generation,
            "Android VOICEVOX One-Wave runtime is unavailable.");
        return false;
#else
        LogErrorForCurrent(
            generation,
            "No VOICEVOX provider is available on this platform."
        );
        return false;
#endif
    }

    private void CancelCurrentRequestAndInvalidate(string reason)
    {
        int generation = requestGeneration;
        Task<string> abandonedWindowsTask = null;
        bool hadActiveRequest =
            requestCancellationGeneration == generation ||
            playbackCoroutineGeneration == generation ||
            windowsSynthesisTaskGeneration == generation ||
            windowsWavOwnerGeneration == generation ||
#if UNITY_ANDROID
            androidSynthesisGeneration == generation ||
            androidWavOwnerGeneration == generation ||
#endif
            audioClipOwnerGeneration == generation ||
            speakingOwnerGeneration == generation;

        if (hadActiveRequest)
        {
            EmitRuntimeSpeechLifecycle(
                generation,
                SpeechAdapterLifecycle.PlaybackInterrupted,
                "REQUEST_CANCELLED:" + (reason ?? string.Empty));
        }

        if (requestCancellationGeneration == generation &&
            requestCancellation != null)
        {
            requestCancellation.Cancel();
        }

        if (windowsSynthesisTaskGeneration == generation &&
            windowsSynthesisTask != null)
        {
            abandonedWindowsTask = windowsSynthesisTask;
            windowsSynthesisTask = null;
            windowsSynthesisTaskGeneration = -1;
        }

        if (playbackCoroutineGeneration == generation &&
            playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
            playbackCoroutineGeneration = -1;
        }

        if (windowsWavOwnerGeneration == generation)
        {
            DeleteCurrentGeneratedWav(windowsWavPath, generation);
            windowsWavPath = null;
            windowsWavOwnerGeneration = -1;
        }

#if UNITY_ANDROID
        if (androidWavOwnerGeneration == generation)
        {
            DeleteCurrentGeneratedWav(androidWavPath, generation);
            androidWavPath = null;
            androidWavOwnerGeneration = -1;
        }

        ClearAndroidSynthesisPresentation(generation);
#endif

        StopAndReleaseAudioClip(generation);
        RestoreSpeakingStateIfOwned(generation);
        DisposeCancellationIfOwned(generation);

        if (logVerbose && hadActiveRequest)
        {
            Debug.Log(
                $"[VoicePlaybackController] Request cancelled. " +
                $"generation={generation} reason={reason}"
            );
        }

        requestGeneration = NextGeneration(generation);

        // Start observing only after invalidation. If the Task had already
        // completed, its continuation still sees an old generation and can
        // safely delete the abandoned result instead of leaking the WAV.
        if (abandonedWindowsTask != null)
        {
            ObserveAbandonedWindowsTask(
                abandonedWindowsTask,
                generation
            );
        }
    }

    private void BeginRuntimeSpeechLifecycle(
        int generation,
        ResponseEnvelope envelope)
    {
        runtimeSpeechGeneration = generation;
        runtimeSpeechId =
            "voice-playback:" + GetInstanceID() + ":" + generation;
        runtimeSpeechTerminal = false;
        runtimeResponseCorrelationId = envelope != null
            ? envelope.OutputId
            : string.Empty;
        if (envelope != null && envelope.HasConversationCorrelation)
        {
            bool attached = ConversationTurnLifecycleRuntime.AttachSpeech(
                envelope.TurnId,
                envelope.OutputId,
                envelope.InteractionId,
                runtimeSpeechId);
            if (!attached)
            {
                ConversationTurnLifecycleRuntime.FailTurn(
                    envelope.TurnId,
                    "speech-correlation-attach-failed");
            }
        }
        EmitRuntimeSpeechLifecycle(
            generation,
            SpeechAdapterLifecycle.RequestAccepted,
            string.Empty);
    }

    private void EmitRuntimeSpeechLifecycle(
        int generation,
        SpeechAdapterLifecycle lifecycle,
        string failureReason)
    {
        if (!IsCurrentGeneration(generation) ||
            runtimeSpeechGeneration != generation ||
            string.IsNullOrEmpty(runtimeSpeechId))
        {
            return;
        }

        bool terminal =
            lifecycle == SpeechAdapterLifecycle.PlaybackCompleted ||
            lifecycle == SpeechAdapterLifecycle.PlaybackFailed ||
            lifecycle == SpeechAdapterLifecycle.PlaybackInterrupted;
        if (runtimeSpeechTerminal)
            return;

        SpeechPlaybackRuntimeFact fact =
            new SpeechPlaybackRuntimeFact(
                runtimeSpeechId,
                generation,
                lifecycle,
                failureReason,
                runtimeResponseCorrelationId);

        ConversationTurnLifecycleRuntime.ObserveSpeechLifecycle(fact);

        if (terminal)
            runtimeSpeechTerminal = true;

        try
        {
            SpeechLifecycleObserved?.Invoke(fact);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[VoicePlaybackController] " +
                "SpeechLifecycleObserved exception: " + exception);
        }
    }

    private async void ObserveAbandonedWindowsTask(
        Task<string> task,
        int abandonedGeneration
    )
    {
        if (task == null)
            return;

        try
        {
            string wavPath = await task;
            DeleteStaleGeneratedWav(wavPath, abandonedGeneration, true);
        }
        catch (OperationCanceledException)
        {
            // Cancellation was requested before the native call began.
        }
        catch (Exception exception)
        {
            // Accessing/awaiting the exception is intentional: no abandoned
            // Task is allowed to leave an unobserved exception behind. Errors
            // from an old generation must not replace current UI/error state.
            string failedWavPath = null;

            if (WindowsVoicevoxSynthesisProvider.TryGetGeneratedOutputPath(
                    exception,
                    out failedWavPath
                ))
            {
                DeleteStaleGeneratedWav(
                    failedWavPath,
                    abandonedGeneration,
                    true
                );
            }

            if (IsCurrentGeneration(abandonedGeneration))
            {
                Debug.LogError(
                    "[VoicePlaybackController] Abandoned Windows task failed: " +
                    exception
                );
            }
        }
    }

    private void CompleteCurrentRequest(
        int generation,
        string wavPath,
        bool restoreSpeaking,
        bool deleteGeneratedWavAfterUse)
    {
        EmitRuntimeSpeechLifecycle(
            generation,
            SpeechAdapterLifecycle.PlaybackCompleted,
            string.Empty);
        FinishCurrentRequest(
            generation, wavPath, restoreSpeaking,
            deleteGeneratedWavAfterUse);
    }

    private void FailCurrentRequest(
        int generation,
        string wavPath,
        bool restoreSpeaking,
        bool deleteGeneratedWavAfterUse,
        string failureReason)
    {
        EmitRuntimeSpeechLifecycle(
            generation,
            SpeechAdapterLifecycle.PlaybackFailed,
            failureReason);
        FinishCurrentRequest(
            generation, wavPath, restoreSpeaking,
            deleteGeneratedWavAfterUse);
    }

    private void InterruptCurrentRequest(
        int generation,
        string wavPath,
        bool restoreSpeaking,
        bool deleteGeneratedWavAfterUse,
        string failureReason)
    {
        EmitRuntimeSpeechLifecycle(
            generation,
            SpeechAdapterLifecycle.PlaybackInterrupted,
            failureReason);
        FinishCurrentRequest(
            generation, wavPath, restoreSpeaking,
            deleteGeneratedWavAfterUse);
    }

    private void FinishCurrentRequest(
        int generation,
        string wavPath,
        bool restoreSpeaking,
        bool deleteGeneratedWavAfterUse = false
    )
    {
        if (!IsCurrentGeneration(generation))
            return;

        if (deleteGeneratedWavAfterUse)
        {
            DeleteCurrentGeneratedWav(wavPath, generation);

            if (windowsWavOwnerGeneration == generation)
            {
                windowsWavPath = null;
                windowsWavOwnerGeneration = -1;
            }
        }

        StopAndReleaseAudioClip(generation);

        if (restoreSpeaking)
            RestoreSpeakingStateIfOwned(generation);

        DisposeCancellationIfOwned(generation);
        ClearWindowsTaskReference(generation);
#if UNITY_ANDROID
        ClearAndroidSynthesisPresentation(generation);
#endif
        ClearPlaybackCoroutineReference(generation);
    }

    private void StopAndReleaseAudioClip(int generation)
    {
        if (!IsCurrentGeneration(generation) ||
            audioClipOwnerGeneration != generation)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.Stop();

            if (audioSource.clip == ownedAudioClip)
                audioSource.clip = null;
        }

        if (ownedAudioClip != null)
            Destroy(ownedAudioClip);

        ownedAudioClip = null;
        audioClipOwnerGeneration = -1;
    }

    private void DisposeCancellationIfOwned(int generation)
    {
        if (!IsCurrentGeneration(generation) ||
            requestCancellationGeneration != generation)
        {
            return;
        }

        if (requestCancellation != null)
        {
            requestCancellation.Dispose();
            requestCancellation = null;
        }

        requestCancellationGeneration = -1;
    }

    private void ClearWindowsTaskReference(int generation)
    {
        if (!IsCurrentGeneration(generation) ||
            windowsSynthesisTaskGeneration != generation)
        {
            return;
        }

        windowsSynthesisTask = null;
        windowsSynthesisTaskGeneration = -1;
    }

    private void ClearPlaybackCoroutineReference(int generation)
    {
        if (!IsCurrentGeneration(generation) ||
            playbackCoroutineGeneration != generation)
        {
            return;
        }

        playbackCoroutine = null;
        playbackCoroutineGeneration = -1;
    }

    private void DeleteCurrentGeneratedWav(
        string wavPath,
        int generation
    )
    {
        if (!IsCurrentGeneration(generation))
            return;

        TryDeleteGeneratedWav(wavPath, generation, "current");
    }

    private void DeleteStaleGeneratedWav(
        string wavPath,
        int generation,
        bool deleteGeneratedWavAfterUse
    )
    {
        if (!deleteGeneratedWavAfterUse || IsCurrentGeneration(generation))
            return;

        TryDeleteGeneratedWav(wavPath, generation, "stale");
    }

    private void TryDeleteGeneratedWav(
        string wavPath,
        int generation,
        string ownership
    )
    {
        if (string.IsNullOrEmpty(wavPath))
            return;

        try
        {
            if (File.Exists(wavPath))
                File.Delete(wavPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[VoicePlaybackController] Temporary WAV delete failed. " +
                $"generation={generation} ownership={ownership} " +
                $"path={wavPath} error={exception.Message}"
            );
        }
    }

    private bool IsCurrentGeneration(int generation)
    {
        return generation == requestGeneration;
    }

    private static int NextGeneration(int generation)
    {
        return generation == int.MaxValue ? 1 : generation + 1;
    }

    private void LogForCurrent(int generation, string message)
    {
        if (!IsCurrentGeneration(generation))
            return;

        Debug.Log("[VoicePlaybackController] " + message);
    }

    private void LogErrorForCurrent(int generation, string message)
    {
        if (!IsCurrentGeneration(generation))
            return;

        Debug.LogError("[VoicePlaybackController] " + message);
    }

    private bool IsVoiceEnabled()
    {
        if (interactionSettings != null && !interactionSettings.EnableVoiceOutput)
            return false;

        if (runtimeSettings == null)
            return true;

        return runtimeSettings.useVoice;
    }

    private bool CanSpeakNow()
    {
        if (limboPermission == null)
            return true;

        if (limboPermission.IsEmergencyMode)
            return false;

        return limboPermission.CanSpeak;
    }

    private bool IsEmergencyBlocked()
    {
        if (limboPermission == null)
            return false;

        return limboPermission.IsEmergencyMode;
    }

    private void EnterSpeakingState(int generation)
    {
        if (!IsCurrentGeneration(generation) || stateController == null)
            return;

        stateBeforeSpeaking = stateController.CurrentState;
        speakingOwnerGeneration = generation;
        stateController.SetSpeaking();
    }

    private void RestoreSpeakingStateIfOwned(int generation)
    {
        if (!IsCurrentGeneration(generation) ||
            speakingOwnerGeneration != generation)
        {
            return;
        }

        speakingOwnerGeneration = -1;

        if (stateController == null)
            return;

        /*
         * A later controller may intentionally have moved the runtime out of
         * Speaking. In that case this request no longer owns the official
         * state and must not overwrite it.
         */
        InteractionState currentState = stateController.CurrentState;

        if (currentState != InteractionState.Speaking)
        {
            if (logVerbose)
            {
                Debug.Log(
                    $"[VoicePlaybackController] ExitSpeakingState skipped. " +
                    $"generation={generation} currentState={currentState} " +
                    $"stateBeforeSpeaking={stateBeforeSpeaking}"
                );
            }

            return;
        }

        if (stateBeforeSpeaking == InteractionState.Listening)
        {
            stateController.SetListening();
            return;
        }

        if (stateBeforeSpeaking == InteractionState.Tracking ||
            stateBeforeSpeaking == InteractionState.TemporaryLost ||
            stateBeforeSpeaking == InteractionState.FullyLost)
        {
            stateController.SetState(stateBeforeSpeaking);
            return;
        }

        stateController.SetIdle();
    }
}
