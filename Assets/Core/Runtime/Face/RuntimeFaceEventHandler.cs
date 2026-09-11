// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.Limbo;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// FaceEvent Runtime入口。
    ///
    /// 責任:
    /// - FaceEventを受け取る
    /// - StableFound / TemporaryLost / FullyLost を分類する
    /// - StableFound時にAutonomousLookAroundPolicyへ再発見を通知する
    /// - RuntimeExpressionControllerへ顔状態由来の表情要求を渡す
    /// - 将来のEvent Reaction Routeへ渡す接続口を保持する
    ///
    /// 禁止:
    /// - FaceEventからBodyActionを直接起動しない
    /// - NeckControllerを直接呼ばない
    /// - CrawlerControllerを直接呼ばない
    /// - ServoControlUnitを直接呼ばない
    ///
    /// 自発探索は IdleTick → AutonomousLookAroundPolicy で判断する。
    /// </summary>
    public sealed class RuntimeFaceEventHandler : MonoBehaviour
    {
        [Header("Runtime Refs")]
        [SerializeField]
        private InteractionStateController stateController;

        [SerializeField]
        private LimboPermission limboPermission;

        [Header("Face Runtime Refs")]
        [SerializeField]
        private AutonomousLookAroundPolicy autonomousLookAroundPolicy;

        [SerializeField]
        private RuntimeExpressionController runtimeExpressionController;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private sealed class FaceRuntimeContext
        {
            public ExternalInputEvent SourceEvent;
            public string FaceState;
            public InteractionState CurrentState;
            public bool HasStateController;
            public bool HasLimboPermission;
            public bool CanThink;
            public bool CanSearch;
            public bool CanStartAction;
            public bool CanInterrupt;
            public bool IsEmergency;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (stateController == null)
            {
                stateController =
                    FindObjectOfType<InteractionStateController>();
            }

            if (limboPermission == null)
            {
                limboPermission =
                    FindObjectOfType<LimboPermission>();
            }

            if (autonomousLookAroundPolicy == null)
            {
                autonomousLookAroundPolicy =
                    GetComponent<AutonomousLookAroundPolicy>();
            }

            if (runtimeExpressionController == null)
            {
                runtimeExpressionController =
                    FindObjectOfType<RuntimeExpressionController>();
            }
        }

        /// <summary>
        /// RuntimeProcessorから呼ぶ正式入口。
        /// </summary>
        public void Process(ExternalInputEvent inputEvent)
        {
            if (inputEvent == null)
            {
                Debug.LogWarning(
                    "[RuntimeFaceEventHandler] FaceEvent blocked: inputEvent is null."
                );

                return;
            }

            FaceRuntimeContext context =
                BuildFaceRuntimeContext(inputEvent);

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeFaceEventHandler] FaceEvent received: " +
                    context.FaceState +
                    " state=" +
                    FormatStateForLog(
                        context.HasStateController,
                        context.CurrentState
                    ) +
                    " canSearch=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.CanSearch
                    ) +
                    " canStartAction=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.CanStartAction
                    ) +
                    " emergency=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.IsEmergency
                    )
                );
            }

            switch (context.FaceState)
            {
                case "StableFound":
                    ProcessFaceStableFound(context);
                    break;

                case "TemporaryLost":
                    ProcessFaceTemporaryLost(context);
                    break;

                case "FullyLost":
                    ProcessFaceFullyLost(context);
                    break;

                default:
                    ProcessUnknownFaceEvent(context);
                    break;
            }
        }

        private FaceRuntimeContext BuildFaceRuntimeContext(
            ExternalInputEvent inputEvent
        )
        {
            bool hasStateController =
                stateController != null;

            bool hasLimboPermission =
                limboPermission != null;

            InteractionState currentState =
                hasStateController
                    ? stateController.CurrentState
                    : InteractionState.Idle;

            return new FaceRuntimeContext
            {
                SourceEvent = inputEvent,

                FaceState =
                    inputEvent != null
                        ? (inputEvent.Payload ?? string.Empty)
                        : string.Empty,

                CurrentState = currentState,

                HasStateController =
                    hasStateController,

                HasLimboPermission =
                    hasLimboPermission,

                CanThink =
                    hasLimboPermission &&
                    limboPermission.CanThink,

                CanSearch =
                    hasLimboPermission &&
                    limboPermission.CanSearch,

                CanStartAction =
                    hasLimboPermission &&
                    limboPermission.CanStartAction,

                CanInterrupt =
                    hasLimboPermission &&
                    limboPermission.CanInterrupt,

                IsEmergency =
                    hasLimboPermission &&
                    limboPermission.IsEmergencyMode
            };
        }

        private void ProcessFaceStableFound(
            FaceRuntimeContext context
        )
        {
            if (autonomousLookAroundPolicy != null)
            {
                autonomousLookAroundPolicy.NotifyFaceStableFound();
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeFaceEventHandler] FaceEvent StableFound: " +
                    "user/face became visible."
                );
            }

            PrepareFaceEventHandoff(
                context,
                "StableFound",
                "expression-runtime: update face state / found_user_reaction candidate"
            );

            RequestExpressionFromFaceEvent(
                "StableFound"
            );
        }

        private void ProcessFaceTemporaryLost(
            FaceRuntimeContext context
        )
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeFaceEventHandler] FaceEvent TemporaryLost: " +
                    "face temporarily lost."
                );
            }

            PrepareFaceEventHandoff(
                context,
                "TemporaryLost",
                "expression-runtime: keep expression / wait shortly / prepare search if loss continues"
            );

            RequestExpressionFromFaceEvent(
                "TemporaryLost"
            );
        }

        private void ProcessFaceFullyLost(
            FaceRuntimeContext context
        )
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeFaceEventHandler] FaceEvent FullyLost: " +
                    "face fully lost."
                );
            }

            PrepareFaceEventHandoff(
                context,
                "FullyLost",
                "expression-runtime: perception-only / expression keep / autonomous search is evaluated from IdleTick"
            );

            RequestExpressionFromFaceEvent(
                "FullyLost"
            );

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeFaceEventHandler] " +
                    "PURE_FACE_EVENT_VERSION active. " +
                    "FullyLost does not start lookAround directly."
                );
            }

            // FaceEvent is a perception/state-change notification only.
            // Do not start BodyAction directly from FaceEvent.
            // Autonomous neck search is evaluated later from:
            // IdleTick + FacePerceptionBuffer + AutonomousLookAroundPolicy.
        }

        private void ProcessUnknownFaceEvent(
            FaceRuntimeContext context
        )
        {
            Debug.LogWarning(
                "[RuntimeFaceEventHandler] Unknown FaceEvent payload: " +
                (
                    context != null
                        ? context.FaceState
                        : "null"
                )
            );
        }

        /// <summary>
        /// 将来のEvent Reaction Route接続口。
        ///
        /// 現在はログ整理のみ。
        /// NeckController / VoiceController / BodyActionExecutorへは進めない。
        /// </summary>
        private void PrepareFaceEventHandoff(
            FaceRuntimeContext context,
            string eventName,
            string futureRoute
        )
        {
            if (context == null)
            {
                Debug.LogWarning(
                    "[RuntimeFaceEventHandler] " +
                    "FaceEvent handoff blocked: context is null."
                );

                return;
            }

            if (context.IsEmergency)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeFaceEventHandler] " +
                        "FaceEvent handoff skipped: emergency mode. " +
                        "event=" +
                        eventName
                    );
                }

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeFaceEventHandler] FaceEvent handoff prepared: " +
                    "event=" +
                    eventName +
                    " state=" +
                    FormatStateForLog(
                        context.HasStateController,
                        context.CurrentState
                    ) +
                    " canSearch=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.CanSearch
                    ) +
                    " canStartAction=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.CanStartAction
                    ) +
                    " canInterrupt=" +
                    FormatBoolForLog(
                        context.HasLimboPermission,
                        context.CanInterrupt
                    ) +
                    " route=" +
                    futureRoute
                );
            }
        }

        private void RequestExpressionFromFaceEvent(
            string faceState
        )
        {
            if (runtimeExpressionController == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeFaceEventHandler] " +
                        "ExpressionRuntime skipped: not assigned. " +
                        "source=FaceEvent faceState=" +
                        SafeLogValue(faceState)
                    );
                }

                return;
            }

            runtimeExpressionController.RequestFromFaceEvent(
                faceState,
                "RuntimeFaceEventHandler FaceEvent"
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

        private static string FormatStateForLog(
            bool hasStateController,
            InteractionState state
        )
        {
            return hasStateController
                ? state.ToString()
                : "Unknown";
        }

        private static string FormatBoolForLog(
            bool hasSource,
            bool value
        )
        {
            return hasSource
                ? value.ToString()
                : "Unknown";
        }
    }
}