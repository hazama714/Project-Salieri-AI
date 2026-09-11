// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

using SalieriAI.Core.Runtime;
using SalieriAI.Core.State;
using SalieriAI.Sensors.Audio;

/// <summary>
/// Phase 10-F:
/// 本番マイク待機制御。
///
/// 役割:
/// - 起動ゲート後、または発話完了後に AndroidSTTReceiver.StartListening() を呼ぶ。
/// - Speaking / Acting / Thinking / Emergency 中は待機を開始しない。
/// - STT結果後、Listening継続中なら会話継続用に再待機する。
/// - STTそのもの、会話判断、状態分類はここに持たない。
/// </summary>
public sealed class SpeechInputController : MonoBehaviour
{
    public enum NoMatchBackoffPauseMode
    {
        PauseUntilManualRestart,
        AutoRecoverAfterDelay
    }

    private const float DefaultReListenDelaySeconds = 0.75f;
    private const float DefaultConversationContinueSeconds = 8.0f;

    [Header("References")]
    [SerializeField]
    private AndroidSTTReceiver sttReceiver;

    [SerializeField]
    private InteractionStateController stateController;

    [SerializeField]
    private RuntimeInteractionSettings interactionSettings;

    [Header("Start Gate")]
    [SerializeField]
    private bool allowStartWhenIdle = true;

    [SerializeField]
    private bool allowStartWhenListening = true;

    [SerializeField]
    private bool blockWhenBusy = true;

    [SerializeField]
    private bool blockWhenEmergency = true;

    [Header("Auto ReListen")]
    [SerializeField]
    private bool autoRelistenAfterSpeech = true;

    [Tooltip("BodyAction / Recovering 終了後に Idle へ戻った場合、STTを自動再待機する。")]
    [SerializeField]
    private bool autoRelistenAfterBodyAction = true;

    [Tooltip("BodyAction後など、auto-relistenでIdleからSTTを再開する時にRuntime状態もListeningへ戻す。")]
    [SerializeField]
    private bool syncStateToListeningWhenRelistenStartsFromIdle = true;

    [Tooltip("Final Result後にSTTを自動再待機する。会話応答待ちを使う場合はOFFでもよい.")]
    [SerializeField]
    private bool autoRelistenAfterSttResult = true;

    [Tooltip("STT Error 5/7 など、認識失敗後にSTTを自動再待機する。Final Result後の再待機とは分離する。")]
    [SerializeField]
    private bool autoRelistenAfterSttError = true;

    [Header("STT Error:7 Backoff")]
    [Tooltip("Android SpeechRecognizer ERROR_NO_MATCH(7) が連続した時、再待機間隔を段階的に延ばし、一定回数を超えたら自動再待機を一時停止する。")]
    [SerializeField]
    private bool enableNoMatchErrorBackoff = true;

    [Tooltip("Error:7 後に自動再待機を許可する最大回数。例: 3 の場合、4回目の連続 Error:7 で自動再待機を一時停止する。")]
    [Min(0)]
    [SerializeField]
    private int maxConsecutiveNoMatchAutoRelisten = 3;

    [Tooltip("1回目の連続 Error:7 後に再待機するまでの秒数。")]
    [Min(0.0f)]
    [SerializeField]
    private float firstNoMatchRelistenDelaySeconds = 1.25f;

    [Tooltip("2回目の連続 Error:7 後に再待機するまでの秒数。")]
    [Min(0.0f)]
    [SerializeField]
    private float secondNoMatchRelistenDelaySeconds = 2.0f;

    [Tooltip("3回目以降の連続 Error:7 後に再待機するまでの秒数。")]
    [Min(0.0f)]
    [SerializeField]
    private float laterNoMatchRelistenDelaySeconds = 5.0f;

    [Tooltip(
        "Error:7 が連続上限を超えた時の扱い。 " +
        "PauseUntilManualRestart は手動操作まで停止。 " +
        "AutoRecoverAfterDelay は一定時間後にSTT再待機を試す。"
    )]
    [SerializeField]
    private NoMatchBackoffPauseMode noMatchBackoffPauseMode =
        NoMatchBackoffPauseMode.AutoRecoverAfterDelay;

    [Tooltip("AutoRecoverAfterDelay 選択時、一時停止後に再待機を試すまでの秒数。")]
    [Min(0.0f)]
    [SerializeField]
    private float noMatchAutoRecoverDelaySeconds = 20.0f;

    [Tooltip("Final Resultを受け取った時、Error:7 の連続回数と一時停止状態を解除する。")]
    [SerializeField]
    private bool resetNoMatchBackoffOnFinalResult = true;

    [Tooltip("Final Result後すぐにSTTを再開せず、ConversationService/LLM応答からSpeakingへ入るのを待つ。")]
    [SerializeField]
    private bool waitForConversationResponseAfterFinalResult = true;

    [Header("Conversation Window")]
    [SerializeField]
    private bool autoIdleWhenListeningTimeout = true;

    [Tooltip("ListeningタイムアウトでSTTを止めた直後に返ってくるAndroid STTの遅延Errorで再待機しないための猶予秒数。")]
    [SerializeField]
    private float suppressErrorRelistenAfterTimeoutStopSeconds = 1.0f;

    [Header("Duplicate Guard")]
    [SerializeField]
    private float minStartIntervalSeconds = 1.0f;

    [Header("Speaking Guard")]
    [SerializeField]
    private bool stopSttWhenEnteringSpeaking = true;

    [SerializeField]
    private bool cancelListeningTimeoutWhenBusy = true;

    [Header("Debug")]
    [SerializeField]
    private bool logEnabled = true;

    private float lastStartRequestTime = -999f;
    private Coroutine relistenCoroutine;
    private Coroutine listeningTimeoutCoroutine;
    private int relistenToken;
    private int listeningTimeoutToken;
    private float suppressSttErrorRelistenUntil = -999f;

    // Phase 10-L:
    // Android SpeechRecognizer ERROR_NO_MATCH(7) が連続した場合の再待機ループ抑制。
    // 生のSTTエラーをRuntime全体の自発行動抑制へ波及させないため、
    // 一定回数を超えたら自動ReListenを一時停止する。
    // Inspector設定により、手動復帰待ちまたは遅延後の自動復帰を選択できる。
    private int consecutiveNoMatchErrorCount;
    private bool noMatchAutoRelistenPaused;

    private Coroutine noMatchAutoRecoverCoroutine;
    private int noMatchAutoRecoverToken;

    // Phase 10-I:
    // Final Result後に「会話応答待ち」として即時ReListenを止めた後、
    // 実際には会話ではなくBodyAction等で処理が完了し、Idleへ落ちるケースを拾うための保留フラグ。
    private bool waitingConversationResponseAfterFinalResult;
    private string waitingConversationResponseText = string.Empty;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        ClearWaitingConversationResponseAfterFinalResult("OnDisable");
        ResetNoMatchErrorBackoff("OnDisable");
        CancelRelisten("OnDisable");
        CancelListeningTimeout("OnDisable");
    }

    /// <summary>
    /// 起動ボタンなどから呼ばれる音声待機開始要求。
    /// 成功したら true、開始しなかったら false。
    /// </summary>
    public bool RequestStartListening(string reason = "manual-start")
    {
        // Phase 10-L:
        // UI起動などの明示的な開始要求は、Error:7 による一時停止の解除操作として扱う。
        ResetNoMatchErrorBackoff("explicit-start:" + reason);

        return RequestStartListeningInternal(
            reason,
            forceAllowListeningState: false,
            ignoreStartIntervalGuard: false
        );
    }

    /// <summary>
    /// ConversationReactionService など、会話継続ウィンドウ側から呼ぶ再待機要求。
    ///
    /// 用途：
    /// - 「アシス」だけを認識して Listening に入った直後、次の発話を拾うために STT を再開する。
    /// - 発話後の継続会話待機など、状態が Listening のままでも安全に再待機したい場合に使う。
    ///
    /// 注意：
    /// - ここでは会話判断はしない。
    /// - 実際に StartListening できるかは、RelistenRoutine 実行時に通常の安全判定で確認する。
    /// </summary>
    public void RequestRelistenForConversationContinuation(string reason = "conversation-continuation")
    {
        ResolveReferences();

        ScheduleRelisten(
            "conversation-continuation:" + reason,
            forceAllowListeningState: true
        );
    }

    /// <summary>
    /// Wake待機 / MicStandby 中に起動語なし発話を無視した後、
    /// STTがFinal Resultで終了したままにならないよう再待機する。
    ///
    /// 用途：
    /// - Idle / MicStandby中に起動語なし発話を拾った
    /// - ConversationReactionService側で「無視」と判断した
    /// - その後も起動語待機を続けたい
    ///
    /// 注意：
    /// - 会話継続用ではないため forceAllowListeningState は false。
    /// - Busy / Speaking / Acting 等なら通常の安全判定で開始しない。
    /// </summary>
    public void RequestRelistenForWakeStandby(string reason = "wake-standby")
    {
        ResolveReferences();

        ScheduleRelisten(
            "wake-standby:" + reason,
            forceAllowListeningState: false
        );
    }

    /// <summary>
    /// 起動ゲート / UI Button 用。
    /// </summary>
    public void RequestStartListeningFromUI()
    {
        RequestStartListening("ui");
    }

    /// <summary>
    /// Error:7 バックオフ停止状態を解除し、STT再待機を要求する。
    ///
    /// 将来のSettingsPanel / Debug UIから呼べる手動復帰入口。
    /// </summary>
    public bool ResumeSttAfterBackoff()
    {
        ResolveReferences();

        ResetNoMatchErrorBackoff(
            "manual-backoff-resume"
        );

        bool forceAllowListeningState =
            stateController != null &&
            stateController.CurrentState ==
            InteractionState.Listening;

        return RequestStartListeningInternal(
            "manual-backoff-resume",
            forceAllowListeningState,
            ignoreStartIntervalGuard: true
        );
    }

    /// <summary>
    /// マイクを明示停止する。
    /// </summary>
    public void RequestStopListening(string reason = "manual-stop")
    {
        ClearWaitingConversationResponseAfterFinalResult("stop:" + reason);
        CancelRelisten("stop:" + reason);
        CancelNoMatchAutoRecover("stop:" + reason);

        if (suppressErrorRelistenAfterTimeoutStopSeconds > 0.0f)
        {
            suppressSttErrorRelistenUntil =
                Time.time + suppressErrorRelistenAfterTimeoutStopSeconds;
        }

        if (sttReceiver != null)
        {
            sttReceiver.StopListening();
        }

        if (logEnabled)
        {
            Debug.Log("[SpeechInputController] Stop requested. reason=" + reason);
        }
    }


    /// <summary>
    /// StartupLockPanel などの起動ゲート側が、
    /// 「新規Startは拒否されたが、すでにSTT開始要求中/Listeningなので運用開始扱いにしてよいか」
    /// を判定するための状態確認。
    ///
    /// StartListening の二重起動を避けるため、RequestStartListening() は false を返す場合がある。
    /// ただし stt-already-requested / Listening 中は、起動不能ではなく「すでに準備中/待機中」として扱う。
    /// </summary>
    public bool IsListeningOrStartRequested()
    {
        ResolveReferences();

        if (sttReceiver != null && sttReceiver.IsListeningRequested)
        {
            return true;
        }

        if (stateController != null &&
            stateController.CurrentState == InteractionState.Listening)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 現在STT開始できるかを外部UIから確認したい場合の入口。
    /// </summary>
    public bool CanStartListening()
    {
        return CanStartListening(out _);
    }

    public bool CanStartListening(out string reason)
    {
        return CanStartListeningInternal(
            out reason,
            forceAllowListeningState: false,
            ignoreStartIntervalGuard: false
        );
    }

    private bool RequestStartListeningInternal(
        string reason,
        bool forceAllowListeningState,
        bool ignoreStartIntervalGuard
    )
    {
        if (!CanStartListeningInternal(
                out string blockReason,
                forceAllowListeningState,
                ignoreStartIntervalGuard
            ))
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] Start skipped. reason=" +
                    blockReason +
                    " requestReason=" +
                    reason +
                    " forceAllowListeningState=" +
                    forceAllowListeningState
                );
            }

            return false;
        }

        STTStartResult startResult = sttReceiver.StartListening();
        if (startResult == null || !startResult.Accepted)
        {
            if (logEnabled)
            {
                Debug.LogWarning(
                    "[SpeechInputController] Provider start rejected. " +
                    "provider=" +
                    (startResult != null
                        ? startResult.ProviderKind.ToString()
                        : "<null>") +
                    " error=" +
                    (startResult != null ? startResult.Error : "<null>"));
            }

            return false;
        }

        lastStartRequestTime = Time.time;

        EnsureListeningStateBeforeSttStart(reason, forceAllowListeningState);

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] Start accepted. reason=" +
                reason +
                " state=" +
                GetStateText() +
                " forceAllowListeningState=" +
                forceAllowListeningState +
                " provider=" + startResult.ProviderKind +
                " disposition=" + startResult.Disposition +
                " recognizerStarted=" +
                startResult.RecognizerActuallyStarted
            );
        }

        return true;
    }

    private bool CanStartListeningInternal(
        out string reason,
        bool forceAllowListeningState,
        bool ignoreStartIntervalGuard
    )
    {
        if (!IsMicrophoneInputEnabled())
        {
            reason = "microphone-input-disabled";
            return false;
        }

        if (sttReceiver == null)
        {
            reason = "stt-receiver-not-assigned";
            return false;
        }

        if (stateController == null)
        {
            reason = "state-controller-not-assigned";
            return false;
        }

        // Android STTへ既にStart要求済みの場合は二重起動しない。
        // ここは状態がListeningかどうかではなく、STT実体側の要求状態を見る。
        if (sttReceiver.IsListeningRequested)
        {
            reason = "stt-already-requested";
            return false;
        }

        InteractionState state = stateController.CurrentState;

        if (blockWhenEmergency && state == InteractionState.Emergency)
        {
            reason = "state-emergency";
            return false;
        }

        if (blockWhenBusy && stateController.IsBusy)
        {
            reason = "state-busy-" + state;
            return false;
        }

        if (state == InteractionState.Booting)
        {
            reason = "state-booting";
            return false;
        }

        if (state == InteractionState.Speaking)
        {
            reason = "state-speaking";
            return false;
        }

        if (state == InteractionState.Acting)
        {
            reason = "state-acting";
            return false;
        }

        if (state == InteractionState.Thinking)
        {
            reason = "state-thinking";
            return false;
        }

        if (state == InteractionState.Listening &&
            !allowStartWhenListening &&
            !forceAllowListeningState)
        {
            reason = "already-listening";
            return false;
        }

        if (state == InteractionState.Idle && !allowStartWhenIdle)
        {
            reason = "idle-start-disabled";
            return false;
        }

        if (state != InteractionState.Idle && state != InteractionState.Listening)
        {
            reason = "unsupported-state-" + state;
            return false;
        }

        float elapsed = Time.time - lastStartRequestTime;
        if (!ignoreStartIntervalGuard && elapsed < minStartIntervalSeconds)
        {
            reason = "start-interval-guard elapsed=" + elapsed.ToString("0.00");
            return false;
        }

        reason = "ok";
        return true;
    }

    private void ResolveReferences()
    {
        if (sttReceiver == null)
        {
            sttReceiver = FindObjectOfType<AndroidSTTReceiver>();
        }

        if (stateController == null)
        {
            stateController = FindObjectOfType<InteractionStateController>();
        }

        if (interactionSettings == null)
        {
            interactionSettings = FindObjectOfType<RuntimeInteractionSettings>();
        }
    }

    private void SubscribeEvents()
    {
        if (stateController != null)
        {
            stateController.OnStateChanged += HandleStateChanged;
        }

        if (sttReceiver != null)
        {
            sttReceiver.OnFinalResultReceived += HandleSttFinalResult;
            sttReceiver.OnErrorReceived += HandleSttError;
            sttReceiver.OnListeningEnded += HandleSttListeningEnded;
        }
    }

    private void UnsubscribeEvents()
    {
        if (stateController != null)
        {
            stateController.OnStateChanged -= HandleStateChanged;
        }

        if (sttReceiver != null)
        {
            sttReceiver.OnFinalResultReceived -= HandleSttFinalResult;
            sttReceiver.OnErrorReceived -= HandleSttError;
            sttReceiver.OnListeningEnded -= HandleSttListeningEnded;
        }
    }

    private void HandleStateChanged(InteractionState previous, InteractionState next)
    {
        if (next == InteractionState.Speaking ||
            next == InteractionState.Acting ||
            next == InteractionState.Thinking ||
            next == InteractionState.Emergency ||
            next == InteractionState.Booting)
        {
            CancelRelisten("state-entered-" + next);

            if (cancelListeningTimeoutWhenBusy)
            {
                CancelListeningTimeout("state-entered-" + next);
            }

            if (next == InteractionState.Speaking)
            {
                ClearWaitingConversationResponseAfterFinalResult("state-entered-Speaking");
                StopSttForSpeaking();
            }

            return;
        }

        if (next == InteractionState.Listening)
        {
            StartListeningTimeout("state-listening");

            if (previous == InteractionState.Speaking && autoRelistenAfterSpeech)
            {
                ScheduleRelisten(
                    "speaking-finished-to-listening",
                    forceAllowListeningState: true
                );
            }

            return;
        }

        if (next == InteractionState.Idle)
        {
            CancelListeningTimeout("state-idle");

            if (previous == InteractionState.Speaking && autoRelistenAfterSpeech)
            {
                ScheduleRelisten(
                    "speaking-finished-to-idle",
                    forceAllowListeningState: false
                );
                return;
            }

            if ((previous == InteractionState.Recovering ||
                 previous == InteractionState.Acting) &&
                autoRelistenAfterBodyAction)
            {
                ScheduleRelisten(
                    "body-action-finished-to-idle",
                    forceAllowListeningState: false
                );
                return;
            }

            if (previous == InteractionState.Listening &&
                waitingConversationResponseAfterFinalResult &&
                autoRelistenAfterBodyAction)
            {
                ClearWaitingConversationResponseAfterFinalResult(
                    "listening-to-idle-after-final-result"
                );

                ScheduleRelisten(
                    "listening-idle-after-final-result-body-action",
                    forceAllowListeningState: false
                );
                return;
            }
        }
    }

    private void HandleSttFinalResult(string text)
    {
        if (resetNoMatchBackoffOnFinalResult)
        {
            ResetNoMatchErrorBackoff("stt-final-result");
        }

        CancelRelisten("stt-final-result");
        CancelListeningTimeout("stt-final-result");

        if (stateController == null)
        {
            return;
        }

        InteractionState state = stateController.CurrentState;

        if (state != InteractionState.Listening)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] Final result received outside Listening. " +
                    "state=" +
                    state +
                    " text=" +
                    text
                );
            }

            return;
        }

        StartListeningTimeout("stt-final-result-listening");

        if (waitForConversationResponseAfterFinalResult)
        {
            MarkWaitingConversationResponseAfterFinalResult(text);

            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen skipped after final result. " +
                    "reason=wait-conversation-response state=Listening"
                );
            }

            return;
        }

        if (!autoRelistenAfterSttResult)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen skipped after final result. " +
                    "reason=auto-relisten-after-final-result-disabled state=Listening"
                );
            }

            return;
        }

        ScheduleRelisten(
            "stt-final-result-listening",
            forceAllowListeningState: true
        );
    }

    private void HandleSttError(string error)
    {
        if (!autoRelistenAfterSttError)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen skipped after STT error. " +
                    "reason=auto-relisten-after-stt-error-disabled error=" +
                    error
                );
            }

            return;
        }

        if (Time.time < suppressSttErrorRelistenUntil)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen skipped after STT error. " +
                    "reason=suppressed-after-timeout-stop error=" +
                    error
                );
            }

            return;
        }

        if (stateController == null)
        {
            return;
        }

        InteractionState state = stateController.CurrentState;
        if (state != InteractionState.Idle && state != InteractionState.Listening)
        {
            return;
        }

        // Phase 10-L:
        // Android SpeechRecognizer.ERROR_NO_MATCH == 7。
        // 無音・認識不成立が続く環境で即時ReListenを繰り返すと、
        // AutonomousClockの抑制が連続してIdleTickを出せなくなる。
        // Error:7だけは段階的バックオフを掛け、一定回数後に自動再待機を止める。
        if (enableNoMatchErrorBackoff && IsSttErrorCode(error, 7))
        {
            HandleNoMatchSttError(
                error,
                forceAllowListeningState: state == InteractionState.Listening
            );
            return;
        }

        ScheduleRelisten(
            "stt-error",
            forceAllowListeningState: state == InteractionState.Listening
        );
    }

    private void HandleNoMatchSttError(
        string error,
        bool forceAllowListeningState
    )
    {
        consecutiveNoMatchErrorCount++;

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] STT Error:7 consecutive=" +
                consecutiveNoMatchErrorCount +
                " error=" +
                error
            );
        }

        if (consecutiveNoMatchErrorCount > maxConsecutiveNoMatchAutoRelisten)
        {
            noMatchAutoRelistenPaused = true;

            CancelRelisten(
                "stt-error-7-backoff-paused"
            );

            if (
                noMatchBackoffPauseMode ==
                NoMatchBackoffPauseMode.AutoRecoverAfterDelay
            )
            {
                ScheduleNoMatchAutoRecover(
                    forceAllowListeningState
                );
            }

            if (logEnabled)
            {
                Debug.LogWarning(
                    "[SpeechInputController] Auto ReListen paused. " +
                    "reason=stt-error-7-backoff " +
                    "mode=" +
                    noMatchBackoffPauseMode +
                    " consecutive=" +
                    consecutiveNoMatchErrorCount +
                    " limit=" +
                    maxConsecutiveNoMatchAutoRelisten +
                    " recoverDelay=" +
                    (
                        noMatchBackoffPauseMode ==
                        NoMatchBackoffPauseMode.AutoRecoverAfterDelay
                            ? noMatchAutoRecoverDelaySeconds.ToString("0.00")
                            : "manual-only"
                    )
                );
            }

            return;
        }

        if (noMatchAutoRelistenPaused)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen skipped after STT Error:7. " +
                    "reason=stt-error-7-backoff-paused consecutive=" +
                    consecutiveNoMatchErrorCount
                );
            }

            return;
        }

        float delay = GetNoMatchRelistenDelaySeconds(consecutiveNoMatchErrorCount);

        ScheduleRelisten(
            "stt-error-7-backoff",
            forceAllowListeningState,
            delay
        );
    }

    private float GetNoMatchRelistenDelaySeconds(int consecutiveCount)
    {
        if (consecutiveCount <= 1)
        {
            return firstNoMatchRelistenDelaySeconds;
        }

        if (consecutiveCount == 2)
        {
            return secondNoMatchRelistenDelaySeconds;
        }

        return laterNoMatchRelistenDelaySeconds;
    }

    private void ScheduleNoMatchAutoRecover(
        bool forceAllowListeningState
    )
    {
        CancelNoMatchAutoRecover(
            "restart"
        );

        int token =
            ++noMatchAutoRecoverToken;

        float delay =
            Mathf.Max(
                0.0f,
                noMatchAutoRecoverDelaySeconds
            );

        noMatchAutoRecoverCoroutine =
            StartCoroutine(
                NoMatchAutoRecoverRoutine(
                    token,
                    delay,
                    forceAllowListeningState
                )
            );

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] " +
                "STT Error:7 auto recovery scheduled. " +
                "delay=" +
                delay.ToString("0.00") +
                " token=" +
                token
            );
        }
    }

    private IEnumerator NoMatchAutoRecoverRoutine(
        int token,
        float delay,
        bool forceAllowListeningState
    )
    {
        if (delay > 0.0f)
        {
            yield return new WaitForSeconds(
                delay
            );
        }

        noMatchAutoRecoverCoroutine = null;

        if (token != noMatchAutoRecoverToken)
        {
            yield break;
        }

        if (!noMatchAutoRelistenPaused)
        {
            yield break;
        }

        if (
            noMatchBackoffPauseMode !=
            NoMatchBackoffPauseMode.AutoRecoverAfterDelay
        )
        {
            yield break;
        }

        if (!CanStartListeningInternal(
                out string blockReason,
                forceAllowListeningState,
                ignoreStartIntervalGuard: true
            ))
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] " +
                    "STT Error:7 auto recovery postponed. " +
                    "reason=" +
                    blockReason
                );
            }

            ScheduleNoMatchAutoRecover(
                forceAllowListeningState
            );

            yield break;
        }

        ResetNoMatchErrorBackoff(
            "stt-error-7-auto-recover"
        );

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] " +
                "STT Error:7 auto recovery attempt."
            );
        }

        RequestStartListeningInternal(
            "stt-error-7-auto-recover",
            forceAllowListeningState,
            ignoreStartIntervalGuard: true
        );
    }

    private void CancelNoMatchAutoRecover(
        string reason
    )
    {
        noMatchAutoRecoverToken++;

        if (noMatchAutoRecoverCoroutine == null)
        {
            return;
        }

        StopCoroutine(
            noMatchAutoRecoverCoroutine
        );

        noMatchAutoRecoverCoroutine = null;

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] " +
                "STT Error:7 auto recovery cancelled. " +
                "reason=" +
                reason
            );
        }
    }

    private void ResetNoMatchErrorBackoff(string reason)
    {
        CancelNoMatchAutoRecover(
            "backoff-reset:" + reason
        );

        if (consecutiveNoMatchErrorCount == 0 && !noMatchAutoRelistenPaused)
        {
            return;
        }

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] STT Error:7 backoff reset. " +
                "reason=" +
                reason +
                " previousConsecutive=" +
                consecutiveNoMatchErrorCount +
                " wasPaused=" +
                noMatchAutoRelistenPaused
            );
        }

        consecutiveNoMatchErrorCount = 0;
        noMatchAutoRelistenPaused = false;
    }

    private static bool IsSttErrorCode(string error, int targetCode)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        int value = 0;
        bool readingDigits = false;

        for (int i = 0; i <= error.Length; i++)
        {
            bool isDigit = i < error.Length && char.IsDigit(error[i]);

            if (isDigit)
            {
                readingDigits = true;
                value = (value * 10) + (error[i] - '0');
                continue;
            }

            if (!readingDigits)
            {
                continue;
            }

            if (value == targetCode)
            {
                return true;
            }

            value = 0;
            readingDigits = false;
        }

        return false;
    }

    private void HandleSttListeningEnded()
    {
        if (logEnabled)
        {
            Debug.Log("[SpeechInputController] STT listening ended.");
        }
    }

    private void EnsureListeningStateBeforeSttStart(
        string reason,
        bool forceAllowListeningState
    )
    {
        if (!syncStateToListeningWhenRelistenStartsFromIdle)
        {
            return;
        }

        if (stateController == null)
        {
            return;
        }

        if (stateController.CurrentState != InteractionState.Idle)
        {
            return;
        }

        if (!ShouldEnterListeningStateForStart(reason, forceAllowListeningState))
        {
            return;
        }

        if (TrySetInteractionState(InteractionState.Listening))
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] Runtime state synced before STT start. " +
                    "Idle -> Listening reason=" +
                    reason +
                    " forceAllowListeningState=" +
                    forceAllowListeningState
                );
            }
        }
        else if (logEnabled)
        {
            Debug.LogWarning(
                "[SpeechInputController] Runtime state sync failed before STT start. " +
                "target=Listening reason=" +
                reason
            );
        }
    }

    private bool ShouldEnterListeningStateForStart(
        string reason,
        bool forceAllowListeningState
    )
    {
        if (forceAllowListeningState)
        {
            return true;
        }

        if (string.IsNullOrEmpty(reason))
        {
            return false;
        }

        // 起動ボタン直後は Wake 待機としてIdleのままでもよい。
        // ここでは「会話/BodyAction後の自動復帰」だけをListeningへ同期する。
        return reason.Contains("auto-relisten:body-action-finished-to-idle") ||
               reason.Contains("auto-relisten:listening-idle-after-final-result-body-action") ||
               reason.Contains("auto-relisten:speaking-finished-to-idle") ||
               reason.Contains("auto-relisten:speaking-finished-to-listening") ||
               reason.Contains("auto-relisten:conversation-continuation");
    }

    private bool TrySetInteractionState(InteractionState target)
    {
        if (stateController == null)
        {
            return false;
        }

        try
        {
            System.Type controllerType = stateController.GetType();

            System.Reflection.MethodInfo setStateMethod =
                controllerType.GetMethod(
                    "SetState",
                    new System.Type[] { typeof(InteractionState) }
                );

            if (setStateMethod != null)
            {
                setStateMethod.Invoke(
                    stateController,
                    new object[] { target }
                );
                return true;
            }

            if (target == InteractionState.Listening)
            {
                System.Reflection.MethodInfo setListeningMethod =
                    controllerType.GetMethod(
                        "SetListening",
                        System.Type.EmptyTypes
                    );

                if (setListeningMethod != null)
                {
                    setListeningMethod.Invoke(stateController, null);
                    return true;
                }
            }
        }
        catch (System.Exception ex)
        {
            if (logEnabled)
            {
                Debug.LogWarning(
                    "[SpeechInputController] TrySetInteractionState failed. " +
                    "target=" +
                    target +
                    " error=" +
                    ex.Message
                );
            }
        }

        return false;
    }

    private void MarkWaitingConversationResponseAfterFinalResult(string text)
    {
        waitingConversationResponseAfterFinalResult = true;
        waitingConversationResponseText = text ?? string.Empty;

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] Conversation response wait marked. " +
                "reason=stt-final-result text=" +
                waitingConversationResponseText
            );
        }
    }

    private void ClearWaitingConversationResponseAfterFinalResult(string reason)
    {
        if (!waitingConversationResponseAfterFinalResult &&
            string.IsNullOrEmpty(waitingConversationResponseText))
        {
            return;
        }

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] Conversation response wait cleared. " +
                "reason=" +
                reason +
                " text=" +
                waitingConversationResponseText
            );
        }

        waitingConversationResponseAfterFinalResult = false;
        waitingConversationResponseText = string.Empty;
    }

    private void StopSttForSpeaking()
    {
        if (!stopSttWhenEnteringSpeaking)
        {
            return;
        }

        if (sttReceiver == null)
        {
            return;
        }

        if (!sttReceiver.IsListeningRequested)
        {
            return;
        }

        sttReceiver.StopListening();

        if (logEnabled)
        {
            Debug.Log("[SpeechInputController] STT stopped because state entered Speaking.");
        }
    }

    private void ScheduleRelisten(
        string reason,
        bool forceAllowListeningState
    )
    {
        ScheduleRelisten(
            reason,
            forceAllowListeningState,
            GetReListenDelaySeconds()
        );
    }

    private void ScheduleRelisten(
        string reason,
        bool forceAllowListeningState,
        float delaySeconds
    )
    {
        // Phase 10-L-2:
        // Error:7 の連続発生で Auto ReListen を停止した後は、
        // BodyAction終了やSpeaking終了など別経路からの自動再待機も止める。
        //
        // UI起動などの明示的な再開は RequestStartListening() 側で
        // ResetNoMatchErrorBackoff() を通して解除する。
        if (noMatchAutoRelistenPaused)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen skipped. " +
                    "reason=stt-error-7-backoff-paused " +
                    "requestReason=" +
                    reason +
                    " consecutive=" +
                    consecutiveNoMatchErrorCount
                );
            }

            return;
        }

        CancelRelisten("restart:" + reason);

        int token = ++relistenToken;
        float delay = Mathf.Max(0.0f, delaySeconds);

        relistenCoroutine = StartCoroutine(
            RelistenRoutine(
                token,
                delay,
                reason,
                forceAllowListeningState
            )
        );

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] ReListen scheduled. " +
                "delay=" +
                delay.ToString("0.00") +
                " reason=" +
                reason +
                " token=" +
                token +
                " forceAllowListeningState=" +
                forceAllowListeningState
            );
        }
    }

    private IEnumerator RelistenRoutine(
        int token,
        float delay,
        string reason,
        bool forceAllowListeningState
    )
    {
        if (delay > 0.0f)
        {
            yield return new WaitForSeconds(delay);
        }

        relistenCoroutine = null;

        if (token != relistenToken)
        {
            yield break;
        }

        // Phase 10-L-2:
        // 待機中にError:7停止状態へ入った場合も、古いCoroutineから再起動しない。
        if (noMatchAutoRelistenPaused)
        {
            if (logEnabled)
            {
                Debug.Log(
                    "[SpeechInputController] ReListen routine skipped. " +
                    "reason=stt-error-7-backoff-paused " +
                    "requestReason=" +
                    reason +
                    " consecutive=" +
                    consecutiveNoMatchErrorCount
                );
            }

            yield break;
        }

        RequestStartListeningInternal(
            "auto-relisten:" + reason,
            forceAllowListeningState,
            ignoreStartIntervalGuard: true
        );
    }

    private void CancelRelisten(string reason)
    {
        relistenToken++;

        if (relistenCoroutine == null)
        {
            return;
        }

        StopCoroutine(relistenCoroutine);
        relistenCoroutine = null;

        if (logEnabled)
        {
            Debug.Log("[SpeechInputController] ReListen cancelled. reason=" + reason);
        }
    }

    private void StartListeningTimeout(string reason)
    {
        if (!autoIdleWhenListeningTimeout)
        {
            return;
        }

        if (stateController == null)
        {
            return;
        }

        if (stateController.CurrentState != InteractionState.Listening)
        {
            return;
        }

        CancelListeningTimeout("restart:" + reason);

        int token = ++listeningTimeoutToken;
        float seconds = GetConversationContinueSeconds();

        listeningTimeoutCoroutine = StartCoroutine(
            ListeningTimeoutRoutine(token, seconds, reason)
        );

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] Listening auto-idle timeout started. " +
                "seconds=" +
                seconds.ToString("0.00") +
                " reason=" +
                reason +
                " token=" +
                token
            );
        }
    }

    private IEnumerator ListeningTimeoutRoutine(
        int token,
        float seconds,
        string reason
    )
    {
        yield return new WaitForSeconds(seconds);

        listeningTimeoutCoroutine = null;

        if (token != listeningTimeoutToken)
        {
            yield break;
        }

        if (stateController == null)
        {
            yield break;
        }

        if (stateController.CurrentState != InteractionState.Listening)
        {
            yield break;
        }

        if (logEnabled)
        {
            Debug.Log(
                "[SpeechInputController] Listening auto-idle timeout elapsed. " +
                "Listening -> Idle/MicStandby reason=" +
                reason +
                " sttKeepAlive=true"
            );
        }

        // Phase 10-F:
        // ここではSTTを止めない。
        // 20秒タイムアウトは「会話継続待機の終了」であり、
        // 「マイク待機の終了」ではない。
        //
        // Idleへ戻すことで ConversationReactionService 側では
        // 起動語なし発話は continuation として受け付けなくなる。
        // ただし Android STT 自体は継続させるため、
        // Wake待機として「アシス」などの起動語は拾える。
        stateController.SetIdle();
    }


    private void CancelListeningTimeout(string reason)
    {
        listeningTimeoutToken++;

        if (listeningTimeoutCoroutine == null)
        {
            return;
        }

        StopCoroutine(listeningTimeoutCoroutine);
        listeningTimeoutCoroutine = null;

        if (logEnabled)
        {
            Debug.Log("[SpeechInputController] Listening timeout cancelled. reason=" + reason);
        }
    }

    private bool IsMicrophoneInputEnabled()
    {
        if (interactionSettings == null)
        {
            return true;
        }

        return interactionSettings.EnableMicrophoneInput;
    }

    private float GetReListenDelaySeconds()
    {
        if (interactionSettings != null)
        {
            return interactionSettings.ReListenDelaySeconds;
        }

        return DefaultReListenDelaySeconds;
    }

    private float GetConversationContinueSeconds()
    {
        if (interactionSettings != null)
        {
            return interactionSettings.ConversationContinueSeconds;
        }

        return DefaultConversationContinueSeconds;
    }

    private string GetStateText()
    {
        if (stateController == null)
        {
            return "<null>";
        }

        return stateController.CurrentState.ToString();
    }
}
