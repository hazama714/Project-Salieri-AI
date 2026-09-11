// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.State;
using SalieriAI.Core.Limbo;
using SalieriAI.Core.Execution;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;

namespace SalieriAI.Core.Runtime
{
    public sealed class RuntimeIdleTickHandler : MonoBehaviour
    {
        [Header("Runtime State Refs")]
        [SerializeField]
        private InteractionStateController stateController;

        [SerializeField]
        private LimboPermission limboPermission;

        [Header("Execution")]
        [SerializeField]
        private ExecutionController executionController;

        [SerializeField]
        private ExecutionResourceManager executionResourceManager;

        [Header("Autonomous Search")]
        [SerializeField]
        private AutonomousLookAroundPolicy autonomousLookAroundPolicy;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private ExecutionActionDescriptorRegistry productionRegistry;
        private ExecutionResourceWaitRuntimeWakeupHost productionWakeupHost;
        private ExecutionProductionAdmissionService productionAdmission;
        private ExecutionProductionSubmissionService productionSubmission;

        internal ExecutionActionDescriptorRegistry ProductionRegistry =>
            productionRegistry;
        internal ExecutionResourceWaitRuntimeWakeupHost ProductionWakeupHost =>
            productionWakeupHost;
        internal ExecutionProductionAdmissionService ProductionAdmission =>
            productionAdmission;
        internal ExecutionProductionSubmissionService ProductionSubmission =>
            productionSubmission;

        private sealed class IdleRuntimeContext
        {
            public ExternalInputEvent SourceEvent;
            public InteractionState CurrentState;
            public bool CanThink;
            public bool CanSearch;
            public bool CanStartAction;
            public bool CanInterrupt;
            public bool IsEmergency;
            public string Payload;
        }

        private void OnEnable()
        {
            if (autonomousLookAroundPolicy == null)
            {
                autonomousLookAroundPolicy =
                    GetComponent<AutonomousLookAroundPolicy>();
            }

            if (executionResourceManager == null)
            {
                executionResourceManager =
                    FindObjectOfType<ExecutionResourceManager>(true);
            }

            if (autonomousLookAroundPolicy != null)
            {
                autonomousLookAroundPolicy.ConfigureShadowOrchestration(
                    stateController, executionResourceManager);
            }
        }

        private void OnDisable()
        {
            if (productionWakeupHost != null)
                productionWakeupHost.Dispose();
            productionWakeupHost = null;
            productionAdmission = null;
            productionSubmission = null;
            productionRegistry = null;
        }

        public void Process(ExternalInputEvent inputEvent)
        {
            if (inputEvent == null)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] IdleTick blocked: inputEvent is null."
                );

                return;
            }

            string payload = inputEvent.Payload ?? string.Empty;

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeIdleTickHandler] IdleTick received: " +
                    payload
                );
            }

            ProcessIdleRuntimeTick(inputEvent);
        }

        private void ProcessIdleRuntimeTick(
            ExternalInputEvent inputEvent
        )
        {
            bool hasStateController = stateController != null;
            bool hasLimboPermission = limboPermission != null;

            InteractionState currentState = hasStateController
                ? stateController.CurrentState
                : InteractionState.Idle;

            bool canThink =
                hasLimboPermission &&
                limboPermission.CanThink;

            bool canSearch =
                hasLimboPermission &&
                limboPermission.CanSearch;

            bool canStartAction =
                hasLimboPermission &&
                limboPermission.CanStartAction;

            bool canInterrupt =
                hasLimboPermission &&
                limboPermission.CanInterrupt;

            bool isEmergency =
                hasLimboPermission &&
                limboPermission.IsEmergencyMode;

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeIdleTickHandler] IdleTick runtime check: " +
                    "state=" +
                    FormatStateForLog(
                        hasStateController,
                        currentState
                    ) +
                    " canThink=" +
                    FormatBoolForLog(
                        hasLimboPermission,
                        canThink
                    ) +
                    " canSearch=" +
                    FormatBoolForLog(
                        hasLimboPermission,
                        canSearch
                    ) +
                    " canStartAction=" +
                    FormatBoolForLog(
                        hasLimboPermission,
                        canStartAction
                    ) +
                    " emergency=" +
                    FormatBoolForLog(
                        hasLimboPermission,
                        isEmergency
                    )
                );
            }

            if (!hasStateController)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] IdleTick blocked: " +
                    "InteractionStateController is not assigned."
                );

                return;
            }

            if (!hasLimboPermission)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] IdleTick blocked: " +
                    "LimboPermission is not assigned."
                );

                return;
            }

            if (isEmergency)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeIdleTickHandler] IdleTick blocked: " +
                        "emergency mode."
                    );
                }

                return;
            }

            if (currentState != InteractionState.Idle)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeIdleTickHandler] IdleTick blocked: " +
                        "state is not Idle. state=" +
                        currentState
                    );
                }

                return;
            }

            if (!canThink)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeIdleTickHandler] IdleTick blocked: " +
                        "Limbo CanThink=false."
                    );
                }

                return;
            }

            IdleRuntimeContext context =
                BuildIdleRuntimeContext(
                    inputEvent,
                    currentState,
                    canThink,
                    canSearch,
                    canStartAction,
                    canInterrupt,
                    isEmergency
                );

            PrepareIdleRuntimeHandoff(context);
        }

        private IdleRuntimeContext BuildIdleRuntimeContext(
            ExternalInputEvent inputEvent,
            InteractionState currentState,
            bool canThink,
            bool canSearch,
            bool canStartAction,
            bool canInterrupt,
            bool isEmergency
        )
        {
            return new IdleRuntimeContext
            {
                SourceEvent = inputEvent,
                CurrentState = currentState,
                CanThink = canThink,
                CanSearch = canSearch,
                CanStartAction = canStartAction,
                CanInterrupt = canInterrupt,
                IsEmergency = isEmergency,
                Payload = inputEvent != null
                    ? (inputEvent.Payload ?? string.Empty)
                    : string.Empty
            };
        }

        private void PrepareIdleRuntimeHandoff(
            IdleRuntimeContext context
        )
        {
            if (context == null)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] IdleTick handoff blocked: " +
                    "context is null."
                );

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeIdleTickHandler] IdleTick handoff prepared: " +
                    "state=" +
                    context.CurrentState +
                    " canThink=" +
                    context.CanThink +
                    " canSearch=" +
                    context.CanSearch +
                    " canStartAction=" +
                    context.CanStartAction +
                    " canInterrupt=" +
                    context.CanInterrupt +
                    " emergency=" +
                    context.IsEmergency +
                    " payload=" +
                    context.Payload
                );
            }

            if (
                autonomousLookAroundPolicy != null &&
                autonomousLookAroundPolicy.TryStartFromIdleTick(
                    sourceEvent: context.SourceEvent,
                    currentState: context.CurrentState,
                    hasStateController: stateController != null,
                    hasLimboPermission: limboPermission != null,
                    canSearch: context.CanSearch,
                    canStartAction: context.CanStartAction,
                    isEmergency: context.IsEmergency
                )
            )
            {
                return;
            }

            ExecutionRequest request =
                BuildPreparedIdleExecutionRequest(context);

            LogPreparedExecutionRequest(request);
            TryStartPreparedExecution(request, DateTime.UtcNow);
        }

        private ExecutionRequest BuildPreparedIdleExecutionRequest(
            IdleRuntimeContext context
        )
        {
            ExternalInputType sourceType =
                context.SourceEvent != null
                    ? context.SourceEvent.Type
                    : ExternalInputType.IdleTick;

            int priority =
                context.SourceEvent != null
                    ? (int)context.SourceEvent.Priority
                    : 0;

            return ExecutionRequest.Create(
                actionId: "none",
                sourceEventType: sourceType.ToString(),
                sourcePayload: context.Payload,
                reason:
                    "Phase6-E result-log idle no-op execution request",
                priority: priority
            );
        }

        private void LogPreparedExecutionRequest(
            ExecutionRequest request
        )
        {
            if (!verboseLog)
            {
                return;
            }

            if (request == null)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] " +
                    "ExecutionRequest prepared failed: " +
                    "request is null."
                );

                return;
            }

            Debug.Log(
                "[RuntimeIdleTickHandler] ExecutionRequest prepared: " +
                "action=" +
                request.ActionId +
                " source=" +
                request.SourceEventType +
                " payload=" +
                request.SourcePayload +
                " priority=" +
                request.Priority +
                " requiresBody=" +
                request.RequiresBody +
                " requiresSpeech=" +
                request.RequiresSpeech +
                " requiresExpression=" +
                request.RequiresExpression +
                " requiresDevice=" +
                request.RequiresDevice +
                " reason=" +
                request.Reason
            );
        }

        private void TryStartPreparedExecution(
            ExecutionRequest request,
            DateTime createdAtUtc
        )
        {
            RoutePreparedExecution(request, createdAtUtc);
        }

        internal bool RoutePreparedExecution(
            ExecutionRequest request,
            DateTime createdAtUtc)
        {
            if (request == null)
                return false;

            if (request.ActionId ==
                ExecutionProductionActionDescriptorRegistry.NoOpActionId)
            {
                return SubmitNoOp(request, createdAtUtc);
            }

            if (executionController == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeIdleTickHandler] " +
                        "ExecutionController skipped: " +
                        "not assigned. action=" +
                        request.ActionId
                    );
                }

                return false;
            }

            return executionController.TryStartExecution(request);
        }

        private bool SubmitNoOp(
            ExecutionRequest request,
            DateTime createdAtUtc)
        {
            if (!EnsureProductionSubmission())
                return false;

            ExecutionSubmissionResult result = productionSubmission.Submit(
                request,
                ExecutionRequestProvenance.Idle(),
                ExecutionProductionActionDescriptorRegistry
                    .CreateNoOpCompositionPolicy(),
                createdAtUtc);
            if (result == null || !result.IsStarted)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] NoOp Production " +
                    "Submission rejected. status=" +
                    (result != null
                        ? result.Status.ToString()
                        : "null"));
                return false;
            }

            bool started = productionWakeupHost.ProcessPlanStartUtc(
                createdAtUtc);
            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeIdleTickHandler] NoOp Production " +
                    "Submission started. plan=" +
                    result.ConstructionSpec.PlanId +
                    " generation=" +
                    result.ConstructionSpec.PlanGeneration +
                    " logicalStart=" + started);
            }
            return started;
        }

        private bool EnsureProductionSubmission()
        {
            if (productionSubmission != null &&
                productionWakeupHost != null &&
                productionRegistry != null)
            {
                return true;
            }

            if (stateController == null)
                return false;
            if (executionResourceManager == null)
            {
                executionResourceManager =
                    FindObjectOfType<ExecutionResourceManager>(true);
            }
            if (executionResourceManager == null)
            {
                Debug.LogWarning(
                    "[RuntimeIdleTickHandler] NoOp Production " +
                    "Submission unavailable: ExecutionResourceManager " +
                    "not found.");
                return false;
            }

            productionRegistry =
                ExecutionProductionActionDescriptorRegistry.Create();
            productionWakeupHost =
                new ExecutionResourceWaitRuntimeWakeupHost(
                    stateController,
                    executionResourceManager);
            productionAdmission = new ExecutionProductionAdmissionService(
                productionWakeupHost);
            productionSubmission =
                new ExecutionProductionSubmissionService(
                    productionRegistry,
                    new ExecutionRequestProductionHandoff(
                        productionAdmission),
                    new GuidExecutionProductionSubmissionIdSource());
            return true;
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
