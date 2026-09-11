// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.Language.Runtime;
using SalieriAI.Core.Reflex.Cognitive;
using SalieriAI.Core.State;
using SalieriAI.Core.Limbo;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Project Salieri AI Runtime Processor
    ///
    /// Phase 7-C:
    /// SafeStateRequest を ExecutionController の受け口へ渡す。
    /// ただし、ExecutionController 側でも observe-only であり、Cancel / Stop / State変更 / Reprocess は行わない。
    ///
    /// Phase 7-B:
    /// InterruptArbiter の分類ログを少し強化する。
    /// ただし、まだ observe-only であり、Cancel / Stop / State変更 / Reprocess は行わない。
    ///
    /// Phase 1:
    /// ExternalInputBuffer から来た Event を受け取り、
    /// 既存処理へ安全に中継する Runtime 入口。
    ///
    /// Phase 2:
    /// FaceEvent を StableFound / TemporaryLost / FullyLost に分類する。
    ///
    /// Phase 3:
    /// IdleTick を Runtime Event として受け取り、
    /// InteractionState / LimboPermission を確認したうえで、
    /// 将来の Affordance / Decision 層へ渡す接続口を準備する。
    ///
    /// Phase 4:
    /// RuntimeProcessor 内の分岐を整理し、
    /// Event 種別ごとの正式入口を明確化する。
    ///
    /// Phase 6-E:
    /// ExecutionRequest を正式型として生成し、
    /// action=none の no-op 実行だけを ExecutionController へ渡す。
    /// ExecutionController.ExecutionFinished の監視と分類ログは、
    /// RuntimeExecutionResultObserver へ分離する。
    /// ただし、この段階ではまだ State変更 / 次Action起動 / BodyActionExecutor / VoiceController は呼ばない。
    ///
    /// この段階では、まだ以下は行わない。
    /// - IdleTick から LLM を起動しない
    /// - FaceEvent から直接 Action を起動しない
    /// - DeviceStateChange から直接 Bluetooth / WiFi を操作しない
    /// - Emergency から直接停止処理を実行しない
    /// - InterruptArbiter / SafeState は Phase 7-A では observe-only ログに留める
    /// </summary>
    public sealed class RuntimeProcessor : MonoBehaviour
    {
        [Header("Existing Runtime Route")]
        [SerializeField] private CognitiveReflexController cognitiveReflexController;

        [Header("Runtime State Refs")]
        [SerializeField] private InteractionStateController stateController;
        [SerializeField] private LimboPermission limboPermission;

        [Header("Execution")]
        [SerializeField] private ExecutionController executionController;

        [Header("Face Runtime")]
        [SerializeField]
        private RuntimeFaceEventHandler runtimeFaceEventHandler;

        [Header("Autonomous Search")]
        [SerializeField]
        private AutonomousLookAroundPolicy autonomousLookAroundPolicy;

        [Header("Idle Runtime")]
        [SerializeField]
        private RuntimeIdleTickHandler runtimeIdleTickHandler;

        [Header("Device State Runtime")]
        [SerializeField]
        private RuntimeDeviceStateChangeHandler runtimeDeviceStateChangeHandler;

        [Header("Interrupt / SafeState")]
        [SerializeField] private bool observeInterruptCandidates = true;
        [SerializeField] private bool logInterruptArbiterClassification = true;
        [SerializeField] private bool routeSafeStateRequestsToExecutionController = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private void OnEnable()
        {
            if (runtimeFaceEventHandler == null)
            {
                runtimeFaceEventHandler =
                    GetComponent<RuntimeFaceEventHandler>();
            }

            if (autonomousLookAroundPolicy == null)
            {
                autonomousLookAroundPolicy =
                    GetComponent<AutonomousLookAroundPolicy>();
            }

            if (runtimeIdleTickHandler == null)
            {
                runtimeIdleTickHandler =
                    GetComponent<RuntimeIdleTickHandler>();
            }

            if (runtimeDeviceStateChangeHandler == null)
            {
                runtimeDeviceStateChangeHandler =
                    GetComponent<RuntimeDeviceStateChangeHandler>();
            }
        }

        private sealed class EmergencyRuntimeContext
        {
            public ExternalInputEvent SourceEvent;
            public string Payload;
            public InteractionState CurrentState;
            public bool HasStateController;
            public bool HasLimboPermission;
            public bool CanInterrupt;
            public bool IsEmergency;
        }

        /// <summary>
        /// ExternalInputEvent を処理する正式入口。
        /// Event 種別ごとに専用メソッドへ分岐する。
        /// </summary>
        public void Process(ExternalInputEvent inputEvent)
        {
            if (inputEvent == null)
            {
                Debug.LogWarning("[RuntimeProcessor] inputEvent is null.");
                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeProcessor] process event: " +
                    inputEvent.Type +
                    " priority=" +
                    inputEvent.Priority +
                    " source=" +
                    (string.IsNullOrEmpty(inputEvent.Source) ? "Unknown" : inputEvent.Source) +
                    " payload=" +
                    (inputEvent.Payload ?? string.Empty)
                );
            }

            InteractionTraceLogger.LogRuntimeStage(
                "RUNTIME_PROCESS",
                inputEvent.CommunicationInput,
                inputEvent.Type.ToString()
            );

            ObserveInterruptCandidate(inputEvent);

            switch (inputEvent.Type)
            {
                case ExternalInputType.UserSpeech:
                    ProcessUserSpeech(inputEvent);
                    break;

                case ExternalInputType.FaceEvent:
                    ProcessFaceEvent(inputEvent);
                    break;

                case ExternalInputType.IdleTick:
                    ProcessIdleTick(inputEvent);
                    break;

                case ExternalInputType.DeviceStateChange:
                    ProcessDeviceStateChange(inputEvent);
                    break;

                case ExternalInputType.Emergency:
                    ProcessEmergency(inputEvent);
                    break;

                case ExternalInputType.Touch:
                    ProcessTouch(inputEvent);
                    break;

                case ExternalInputType.Environment:
                    ProcessEnvironment(inputEvent);
                    break;

                case ExternalInputType.None:
                default:
                    ProcessUnhandled(inputEvent);
                    break;
            }
        }

        // ============================================================
        // UserSpeech
        // ============================================================

        /// <summary>
        /// UserSpeech 系入口。
        ///
        /// Phase 4では既存会話反応ルートを維持する。
        /// 将来的には ConversationAction / SpeechDecision へ分離する候補。
        /// </summary>
        private void ProcessUserSpeech(ExternalInputEvent inputEvent)
        {
            if (autonomousLookAroundPolicy != null)
            {
                autonomousLookAroundPolicy.NotifyUserSpeech();
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeProcessor] UserSpeech route: CognitiveReflexController"
                );
            }

            InteractionTraceLogger.LogRuntimeStage(
                "RUNTIME_USER_SPEECH",
                inputEvent.CommunicationInput,
                "CognitiveReflexController"
            );

            if (cognitiveReflexController == null)
            {
                Debug.LogWarning(
                    "[RuntimeProcessor] UserSpeech blocked: CognitiveReflexController is not assigned."
                );
                return;
            }

            cognitiveReflexController.ProcessExternalInput(inputEvent);
        }

        // ============================================================
        // FaceEvent
        // ============================================================

        /// <summary>
        /// FaceEvent系入口。
        ///
        /// 詳細処理はRuntimeFaceEventHandlerへ委譲する。
        /// RuntimeProcessorはイベント振り分けだけを担当する。
        /// </summary>
        private void ProcessFaceEvent(
            ExternalInputEvent inputEvent
        )
        {
            if (runtimeFaceEventHandler == null)
            {
                Debug.LogWarning(
                    "[RuntimeProcessor] FaceEvent blocked: " +
                    "RuntimeFaceEventHandler is not assigned."
                );

                return;
            }

            runtimeFaceEventHandler.Process(
                inputEvent
            );
        }

        // ============================================================
        // IdleTick
        // ============================================================

        /// <summary>
        /// IdleTick 系入口。
        ///
        /// 詳細処理は RuntimeIdleTickHandler へ委譲する。
        /// RuntimeProcessor は Event 振り分けだけを担当する。
        /// </summary>
        private void ProcessIdleTick(
            ExternalInputEvent inputEvent
        )
        {
            if (runtimeIdleTickHandler == null)
            {
                Debug.LogWarning(
                    "[RuntimeProcessor] IdleTick blocked: " +
                    "RuntimeIdleTickHandler is not assigned."
                );

                return;
            }

            runtimeIdleTickHandler.Process(
                inputEvent
            );
        }

        // ============================================================
        // DeviceStateChange
        // ============================================================

        /// <summary>
        /// DeviceStateChange 系入口。
        ///
        /// Bluetooth / WiFi / Voice / Camera / Thermal / Battery などの
        /// 状態変化 Event を受けるための正式入口。
        ///
        /// Phase 4ではログと context 作成のみ。
        /// まだ再接続・停止・復帰処理は行わない。
        /// </summary>
        private void ProcessDeviceStateChange(
            ExternalInputEvent inputEvent
        )
        {
            if (runtimeDeviceStateChangeHandler == null)
            {
                Debug.LogWarning(
                    "[RuntimeProcessor] DeviceStateChange blocked: " +
                    "RuntimeDeviceStateChangeHandler is not assigned."
                );

                return;
            }

            runtimeDeviceStateChangeHandler.Process(
                inputEvent
            );
        }

        // ============================================================
        // Emergency
        // ============================================================

        /// <summary>
        /// Emergency 系入口。
        ///
        /// 将来 InterruptArbiter / SafeState / EmergencyStop へ渡すための入口。
        ///
        /// Phase 4ではまだ直接停止処理はしない。
        /// </summary>
        private void ProcessEmergency(ExternalInputEvent inputEvent)
        {
            if (autonomousLookAroundPolicy != null)
            {
                autonomousLookAroundPolicy.InterruptActiveSearch(
                    "Emergency external input");
            }

            EmergencyRuntimeContext context =
                BuildEmergencyRuntimeContext(inputEvent);

            Debug.LogWarning(
                "[RuntimeProcessor] Emergency received: " +
                "payload=" +
                context.Payload +
                " state=" +
                FormatStateForLog(context.HasStateController, context.CurrentState) +
                " canInterrupt=" +
                FormatBoolForLog(context.HasLimboPermission, context.CanInterrupt) +
                " emergency=" +
                FormatBoolForLog(context.HasLimboPermission, context.IsEmergency)
            );

            PrepareEmergencyHandoff(context);
        }

        private EmergencyRuntimeContext BuildEmergencyRuntimeContext(
            ExternalInputEvent inputEvent
        )
        {
            bool hasStateController = stateController != null;
            bool hasLimboPermission = limboPermission != null;

            InteractionState currentState = hasStateController
                ? stateController.CurrentState
                : InteractionState.Idle;

            return new EmergencyRuntimeContext
            {
                SourceEvent = inputEvent,
                Payload = inputEvent != null
                    ? (inputEvent.Payload ?? string.Empty)
                    : string.Empty,
                CurrentState = currentState,
                HasStateController = hasStateController,
                HasLimboPermission = hasLimboPermission,
                CanInterrupt = hasLimboPermission && limboPermission.CanInterrupt,
                IsEmergency = hasLimboPermission && limboPermission.IsEmergencyMode
            };
        }

        /// <summary>
        /// Emergency の将来接続口。
        ///
        /// 将来ここから以下へ進む候補。
        /// - InterruptArbiter
        /// - SafeStateRequest
        /// - EmergencyState
        /// - ExecutionController cancel / stop
        /// - ManualReset / SafetyRecovery
        ///
        /// Phase 4ではまだ実行しない。
        /// </summary>
        private void PrepareEmergencyHandoff(
            EmergencyRuntimeContext context
        )
        {
            if (context == null)
            {
                Debug.LogWarning(
                    "[RuntimeProcessor] Emergency handoff blocked: context is null."
                );
                return;
            }

            Debug.LogWarning(
                "[RuntimeProcessor] Emergency handoff prepared: " +
                "payload=" +
                context.Payload +
                " route=future InterruptArbiter / SafeState / Emergency handling"
            );
        }

        // ============================================================
        // Other Events
        // ============================================================

        /// <summary>
        /// Touch 系入口。
        /// Phase 4では未処理。
        /// </summary>
        private void ProcessTouch(ExternalInputEvent inputEvent)
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeProcessor] Touch event received but not handled yet: " +
                    "payload=" +
                    (inputEvent.Payload ?? string.Empty)
                );
            }
        }

        /// <summary>
        /// Environment 系入口。
        /// Phase 4では未処理。
        /// </summary>
        private void ProcessEnvironment(ExternalInputEvent inputEvent)
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeProcessor] Environment event received but not handled yet: " +
                    "payload=" +
                    (inputEvent.Payload ?? string.Empty)
                );
            }
        }

        private void ProcessUnhandled(ExternalInputEvent inputEvent)
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeProcessor] Unhandled event: " +
                    inputEvent.Type +
                    " payload=" +
                    (inputEvent.Payload ?? string.Empty)
                );
            }
        }

        // ============================================================
        // Phase 7-C Interrupt / SafeState observe-only route
        // ============================================================

        /// <summary>
        /// Phase 7-B:
        /// 現在の ExternalInputEvent が将来的に割り込み候補になるかを分類し、
        /// InterruptArbiter分類ログとSafeStateRequest preparedログだけを出す。
        ///
        /// このメソッドでは以下を行わない。
        /// - ExecutionController.CancelCurrent()
        /// - InteractionStateController.SetState()
        /// - BodyActionExecutor / VoiceController / NeckController 呼び出し
        /// - Reprocess / 次Action起動
        /// </summary>
        private void ObserveInterruptCandidate(ExternalInputEvent inputEvent)
        {
            if (!observeInterruptCandidates)
            {
                return;
            }

            if (inputEvent == null)
            {
                return;
            }

            bool hasStateController = stateController != null;
            bool hasLimboPermission = limboPermission != null;

            InteractionState currentState = hasStateController
                ? stateController.CurrentState
                : InteractionState.Idle;

            bool canInterrupt = hasLimboPermission && limboPermission.CanInterrupt;
            bool isEmergencyMode = hasLimboPermission && limboPermission.IsEmergencyMode;

            SafeStateRequest safeStateRequest = InterruptArbiter.Classify(
                inputEvent,
                currentState,
                hasStateController,
                hasLimboPermission,
                canInterrupt,
                isEmergencyMode
            );

            if (safeStateRequest == null)
            {
                return;
            }

            LogInterruptArbiterClassification(safeStateRequest);
            LogSafeStateRequestPrepared(safeStateRequest);
            TryRouteSafeStateRequestToExecutionController(safeStateRequest);
        }

        private void TryRouteSafeStateRequestToExecutionController(SafeStateRequest request)
        {
            if (!routeSafeStateRequestsToExecutionController)
            {
                return;
            }

            if (request == null)
            {
                return;
            }

            if (executionController == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeProcessor] SafeStateRequest route skipped: ExecutionController is not assigned. " +
                        "requestId=" +
                        SafeLogValue(request.RequestId) +
                        " interrupt=" +
                        request.InterruptType
                    );
                }

                return;
            }

            bool accepted = executionController.TryRequestSafeState(request);

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeProcessor] SafeStateRequest routed to ExecutionController: " +
                    "requestId=" +
                    SafeLogValue(request.RequestId) +
                    " interrupt=" +
                    request.InterruptType +
                    " accepted=" +
                    accepted +
                    " route=observe-only/no-cancel"
                );
            }
        }

        private void LogInterruptArbiterClassification(SafeStateRequest request)
        {
            if (!logInterruptArbiterClassification)
            {
                return;
            }

            if (request == null)
            {
                return;
            }

            Debug.Log(
                "[InterruptArbiter] classify: " +
                "event=" +
                request.SourceEventType +
                " state=" +
                FormatStateForLog(request.HasStateController, request.CurrentState) +
                " result=" +
                request.InterruptType +
                " reason=" +
                request.Reason +
                " payload=" +
                SafeLogValue(request.SourcePayload) +
                " canInterrupt=" +
                FormatBoolForLog(request.HasLimboPermission, request.CanInterrupt) +
                " emergency=" +
                FormatBoolForLog(request.HasLimboPermission, request.IsEmergencyMode) +
                " route=observe-only/no-cancel"
            );
        }

        private void LogSafeStateRequestPrepared(SafeStateRequest request)
        {
            if (request == null)
            {
                return;
            }

            Debug.Log(
                "[RuntimeProcessor] SafeStateRequest prepared: " +
                "requestId=" +
                SafeLogValue(request.RequestId) +
                " interrupt=" +
                request.InterruptType +
                " reason=" +
                request.Reason +
                " source=" +
                request.SourceEventType +
                " payload=" +
                SafeLogValue(request.SourcePayload) +
                " state=" +
                FormatStateForLog(request.HasStateController, request.CurrentState) +
                " canInterrupt=" +
                FormatBoolForLog(request.HasLimboPermission, request.CanInterrupt) +
                " emergency=" +
                FormatBoolForLog(request.HasLimboPermission, request.IsEmergencyMode) +
                " route=" +
                SafeLogValue(request.Route) +
                " message=" +
                SafeLogValue(request.Message)
            );
        }

        // ============================================================
        // Log Helpers
        // ============================================================

        private string SafeLogValue(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value;
        }

        private string FormatStateForLog(
            bool hasStateController,
            InteractionState state
        )
        {
            return hasStateController
                ? state.ToString()
                : "Unknown";
        }

        private string FormatBoolForLog(
            bool hasValue,
            bool value
        )
        {
            return hasValue
                ? value.ToString()
                : "Unknown";
        }
    }
}
