// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using UnityEngine;

using SalieriAI.Core.Execution;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Input;
using SalieriAI.Core.State;
using SalieriAI.Core.Perception.Buffer;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// 顔を見失った状態が続いた時に、
    /// 自発的な首探索 lookAround を起動してよいか判断する。
    ///
    /// 責任:
    /// - FullyLost の確認
    /// - 顔ロスト継続時間の確認
    /// - UserSpeech直後の抑制
    /// - クールダウン
    /// - 同一顔喪失エピソード内での連発防止
    /// - lookAround ExecutionRequest の生成と起動
    ///
    /// 禁止:
    /// - FaceEvent受信直後に直接Actionを起動しない
    /// - NeckControllerを直接呼ばない
    /// - ServoControlUnitを直接呼ばない
    /// </summary>
    public sealed class AutonomousLookAroundPolicy : MonoBehaviour
    {
        [Header("Runtime References")]
        [SerializeField] private ExecutionController executionController;
        [SerializeField] private FacePerceptionBuffer facePerceptionBuffer;
        [SerializeField] private RuntimeExpressionController runtimeExpressionController;

        [Header("Autonomous LookAround")]
        [SerializeField] private bool enableAutonomousLookAround = true;

        [Tooltip("FullyLost になってから首探索を始めるまでの最低ロスト時間。")]
        [SerializeField] private float minNoFaceDurationBeforeLookAroundSeconds = 3.5f;

        [Tooltip("lookAround 自発探索の最低間隔。")]
        [SerializeField] private float autonomousLookAroundCooldownSeconds = 5.0f;

        [Tooltip("UserSpeech / STT 処理直後に自発探索を抑制する秒数。")]
        [SerializeField] private float suppressLookAroundAfterUserSpeechSeconds = 3.0f;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private const string ActionLookAround = "lookAround";

        private float lastAutonomousLookAroundTime = -999f;
        private float lastUserSpeechEventTime = -999f;
        private NeckActionExecutor neckActionExecutor;
        private AutonomousLookAroundShadowOrchestrationObserver
            shadowOrchestrationObserver;

        public AutonomousLookAroundShadowObservation LastShadowObservation
            { get; private set; }
        public float LastUserSpeechEventTime => lastUserSpeechEventTime;

        public event Action<AutonomousLookAroundShadowObservation>
            ShadowObservationCompleted;

        private void Awake()
        {
            neckActionExecutor = FindObjectOfType<NeckActionExecutor>();
        }

        /// <summary>
        /// Installs the lookAround-only Shadow observer. This does not replace
        /// the serialized Legacy ExecutionController route.
        /// </summary>
        public void ConfigureShadowOrchestration(
            InteractionStateController stateController,
            ExecutionResourceManager resourceManager)
        {
            shadowOrchestrationObserver =
                stateController != null && resourceManager != null
                    ? new AutonomousLookAroundShadowOrchestrationObserver(
                        stateController, resourceManager)
                    : null;
        }

        /// <summary>
        /// 同一の顔喪失中に lookAround を繰り返さないためのフラグ。
        /// StableFound を受けた時だけ解除する。
        /// </summary>
        private bool triggeredForCurrentLoss;

        /// <summary>
        /// UserSpeechを受けた時に呼ぶ。
        /// 会話直後の不自然な自発探索を抑制する。
        /// </summary>
        public void NotifyUserSpeech()
        {
            lastUserSpeechEventTime = Time.time;
            InterruptActiveSearch("Explicit user speech");
        }

        /// <summary>
        /// 顔を再発見した時に呼ぶ。
        /// 新しい顔喪失エピソードで再び探索できるようにする。
        /// </summary>
        public void NotifyFaceStableFound()
        {
            InterruptActiveSearch("FaceStableFound");

            if (!triggeredForCurrentLoss)
            {
                return;
            }

            triggeredForCurrentLoss = false;

            if (verboseLog)
            {
                Debug.Log(
                    "[AutonomousLookAroundPolicy] Loss episode reset: face became visible."
                );
            }
        }

        /// <summary>
        /// Stops only the internal lookAround target-direction request.
        /// It does not cancel Conversation, Object tracking, or physical
        /// transport and does not fabricate physical completion.
        /// </summary>
        public bool InterruptActiveSearch(string reason)
        {
            if (neckActionExecutor == null)
                neckActionExecutor = FindObjectOfType<NeckActionExecutor>();

            return neckActionExecutor != null &&
                neckActionExecutor.InterruptLookAroundSearch(reason);
        }

        /// <summary>
        /// IdleTickから呼ぶ正式入口。
        /// FaceEventからは直接呼ばない。
        /// </summary>
        public bool TryStartFromIdleTick(
            ExternalInputEvent sourceEvent,
            InteractionState currentState,
            bool hasStateController,
            bool hasLimboPermission,
            bool canSearch,
            bool canStartAction,
            bool isEmergency
        )
        {
            string faceState = facePerceptionBuffer != null
                ? facePerceptionBuffer.State.ToString()
                : string.Empty;

            return TryStart(
                trigger: "IdleTick.FullyLost",
                sourceEvent: sourceEvent,
                currentState: currentState,
                hasStateController: hasStateController,
                hasLimboPermission: hasLimboPermission,
                canSearch: canSearch,
                canStartAction: canStartAction,
                isEmergency: isEmergency,
                payloadFaceState: faceState
            );
        }

        private bool TryStart(
            string trigger,
            ExternalInputEvent sourceEvent,
            InteractionState currentState,
            bool hasStateController,
            bool hasLimboPermission,
            bool canSearch,
            bool canStartAction,
            bool isEmergency,
            string payloadFaceState
        )
        {
            if (!enableAutonomousLookAround)
            {
                return false;
            }

            if (isEmergency)
            {
                LogBlocked(trigger, "emergency");
                return false;
            }

            if (!hasStateController)
            {
                LogBlocked(trigger, "state-controller-not-assigned");
                return false;
            }

            if (currentState != InteractionState.Idle)
            {
                LogBlocked(
                    trigger,
                    "state-not-idle state=" + currentState
                );
                return false;
            }

            if (hasLimboPermission && !canSearch)
            {
                LogBlocked(trigger, "limbo-can-search-false");
                return false;
            }

            if (hasLimboPermission && !canStartAction)
            {
                LogBlocked(trigger, "limbo-can-start-action-false");
                return false;
            }

            string faceBlockReason =
                GetFaceBlockReason(payloadFaceState);

            if (!string.IsNullOrEmpty(faceBlockReason))
            {
                LogBlocked(trigger, faceBlockReason);
                return false;
            }

            if (triggeredForCurrentLoss)
            {
                LogBlocked(
                    trigger,
                    "already-triggered-for-current-loss"
                );
                return false;
            }

            float now = Time.time;

            if (now - lastUserSpeechEventTime <
                suppressLookAroundAfterUserSpeechSeconds)
            {
                LogBlocked(
                    trigger,
                    "after-user-speech elapsed=" +
                    (now - lastUserSpeechEventTime).ToString("0.00")
                );

                return false;
            }

            if (now - lastAutonomousLookAroundTime <
                autonomousLookAroundCooldownSeconds)
            {
                LogBlocked(
                    trigger,
                    "cooldown elapsed=" +
                    (now - lastAutonomousLookAroundTime).ToString("0.00")
                );

                return false;
            }

            if (executionController == null)
            {
                LogBlocked(
                    trigger,
                    "execution-controller-not-assigned"
                );

                return false;
            }

            if (!executionController.CanStartExecution())
            {
                LogBlocked(
                    trigger,
                    "execution-controller-busy"
                );

                return false;
            }

            ExecutionRequest request =
                BuildExecutionRequest(sourceEvent, trigger);

            Debug.Log(
                "[AutonomousLookAroundPolicy] Autonomous search candidate selected " +
                "action=lookAround" +
                " trigger=" + trigger +
                " faceState=" + SafeLogValue(payloadFaceState) +
                " noFaceDuration=" + GetNoFaceDurationForLog() +
                " state=" + currentState +
                " canSearch=" + canSearch +
                " canStartAction=" + canStartAction
            );

            bool accepted = RouteLegacyAndShadow(
                request, DateTime.UtcNow);

            if (!accepted)
            {
                return false;
            }

            lastAutonomousLookAroundTime = now;
            triggeredForCurrentLoss = true;

            RequestExpressionFromIdleFullyLost(trigger);

            return true;
        }

        /// <summary>
        /// Sends exactly the same request to the existing Legacy physical
        /// route first, then observes it through the lookAround Shadow route.
        /// Shadow failures never alter the Legacy acceptance result.
        /// </summary>
        public bool RouteLegacyAndShadow(
            ExecutionRequest request,
            DateTime createdAtUtc)
        {
            if (executionController == null)
                return false;

            var bodyFacts = new List<BodyExecutionRuntimeFact>();
            Action<BodyExecutionRuntimeFact> capture = fact =>
            {
                if (fact != null &&
                    fact.ActionId == ExecutionLookAroundContract.ActionId)
                {
                    bodyFacts.Add(fact);
                }
            };

            bool legacyAccepted;
            executionController.BodyLifecycleObserved += capture;
            try
            {
                legacyAccepted =
                    executionController.TryStartExecution(request);
            }
            finally
            {
                executionController.BodyLifecycleObserved -= capture;
            }

            if (!legacyAccepted || shadowOrchestrationObserver == null)
                return legacyAccepted;

            try
            {
                LastShadowObservation =
                    shadowOrchestrationObserver.Observe(
                        request,
                        createdAtUtc,
                        true,
                        bodyFacts);
                ShadowObservationCompleted?.Invoke(
                    LastShadowObservation);

                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousLookAroundPolicy] Shadow observation " +
                        LastShadowObservation);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AutonomousLookAroundPolicy] Shadow observation " +
                    "failed without affecting Legacy execution: " +
                    exception.GetType().Name + ": " + exception.Message);
            }

            return legacyAccepted;
        }

        private string GetFaceBlockReason(
            string payloadFaceState
        )
        {
            if (facePerceptionBuffer != null)
            {
                string bufferState =
                    facePerceptionBuffer.State.ToString();

                float noFaceDuration =
                    facePerceptionBuffer.NoFaceDuration;

                if (!facePerceptionBuffer.IsFullyLost)
                {
                    return
                        "face-not-fully-lost " +
                        "bufferState=" + SafeLogValue(bufferState) +
                        " payloadState=" + SafeLogValue(payloadFaceState);
                }

                if (noFaceDuration <
                    minNoFaceDurationBeforeLookAroundSeconds)
                {
                    return
                        "fully-lost-waiting " +
                        "bufferState=" + SafeLogValue(bufferState) +
                        " noFaceDuration=" +
                        noFaceDuration.ToString("0.00") +
                        " required=" +
                        minNoFaceDurationBeforeLookAroundSeconds.ToString("0.00");
                }

                return string.Empty;
            }

            if (!string.Equals(
                payloadFaceState,
                "FullyLost",
                System.StringComparison.Ordinal
            ))
            {
                return
                    "face-not-fully-lost " +
                    "payloadState=" + SafeLogValue(payloadFaceState) +
                    " buffer=not-assigned";
            }

            return string.Empty;
        }

        private ExecutionRequest BuildExecutionRequest(
            ExternalInputEvent sourceEvent,
            string trigger
        )
        {
            ExternalInputType sourceType =
                sourceEvent != null
                    ? sourceEvent.Type
                    : ExternalInputType.IdleTick;

            int priority =
                sourceEvent != null
                    ? (int)sourceEvent.Priority
                    : 0;

            string payload =
                sourceEvent != null
                    ? (sourceEvent.Payload ?? string.Empty)
                    : string.Empty;

            return ExecutionRequest.Create(
                actionId: ActionLookAround,
                sourceEventType: sourceType.ToString(),
                sourcePayload: payload,
                reason:
                    "Phase10-I-2 autonomous neck search trigger=" +
                    trigger,
                priority: priority
            );
        }

        private void RequestExpressionFromIdleFullyLost(
            string trigger
        )
        {
            if (runtimeExpressionController == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[AutonomousLookAroundPolicy] " +
                        "ExpressionRuntime skipped: not assigned. " +
                        "source=IdleTick trigger=" +
                        SafeLogValue(trigger)
                    );
                }

                return;
            }

            runtimeExpressionController.RequestFromIdleFullyLost(
                "AutonomousLookAroundPolicy trigger=" +
                SafeLogValue(trigger)
            );
        }

        private string GetNoFaceDurationForLog()
        {
            if (facePerceptionBuffer == null)
            {
                return "Unknown";
            }

            return
                facePerceptionBuffer.NoFaceDuration.ToString("0.00");
        }

        private void LogBlocked(
            string trigger,
            string reason
        )
        {
            if (!verboseLog)
            {
                return;
            }

            Debug.Log(
                "[AutonomousLookAroundPolicy] lookAround blocked: " +
                "trigger=" + SafeLogValue(trigger) +
                " reason=" + SafeLogValue(reason)
            );
        }

        private static string SafeLogValue(
            string value
        )
        {
            return string.IsNullOrEmpty(value)
                ? "Unknown"
                : value;
        }
    }
}
