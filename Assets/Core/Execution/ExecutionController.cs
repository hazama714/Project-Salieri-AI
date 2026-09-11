// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections;
using UnityEngine;

using SalieriAI.Core.Perception.Buffer;
using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// ExecutionController is the runtime execution-entry layer.
    ///
    /// Current Phase 10-H responsibility boundary:
    /// - Receive ExecutionRequest from RuntimeProcessor / Decision layer.
    /// - Validate whether the request is allowed in the current prepared execution model.
    /// - Keep IsRunning / CurrentRequest / ExecutionStarted / ExecutionFinished as the
    ///   central execution lifecycle signal.
    /// - For action=none, complete as prepared-only/no-op.
    /// - For a small limited set of body actions, delegate to BodyActionExecutor.
    /// - Observe SafeStateRequest and InterruptibleAction slots without actually cancelling
    ///   or stopping devices yet.
    ///
    /// It must not know servo IDs, Bluetooth commands, concrete neck angles, crawler
    /// command strings, voice playback implementation, or expression details.
    /// Those belong below BodyActionExecutor / part-specific executors / controllers.
    ///
    /// Historical note:
    /// This class started as Phase 6-D prepared-only/no-op execution. Phase 10-H keeps
    /// that safe default, while opening a limited route to BodyActionExecutor for
    /// selected ProgrammedAction IDs.
    /// </summary>
    public sealed class ExecutionController : MonoBehaviour
    {
        private const string AllowedNoOpActionId = "none";
        private const string ActionReturnCenter = "returnCenter";
        private const string ActionIdleNod = "idleNod";
        private const string ActionLightSearch = "lightSearch";
        private const string ActionLookAround = "lookAround";
        private const string ActionSearchUser = "searchUser";
        private const string ActionCrawlerStop = "crawler_stop";
        private const string ActionEmergencyStop = "emergency_stop";
        private const string ActionCrawlerForwardShort = "crawler_forward_short";
        private const string ActionCrawlerBackShort = "crawler_back_short";
        private const string ActionCrawlerTurnLeftShort = "crawler_turn_left_short";
        private const string ActionCrawlerTurnRightShort = "crawler_turn_right_short";

        private static bool IsPreparedOnlyProgrammedAction(string actionId)
        {
            switch (actionId)
            {
                case "lookAtUser":
                case "lookAtUserOrSearch":
                case "searchUser":
                case "lightSearch":
                case "lookAround":
                case "returnCenter":
                case "idleNod":
                case "hold":
                case "stop":
                case "crawler_stop":
                case "emergency_stop":
                case "crawler_forward_short":
                case "crawler_back_short":
                case "crawler_turn_left_short":
                case "crawler_turn_right_short":
                    return true;

                default:
                    return false;
            }
        }

        [Header("Guard")]
        [SerializeField] private bool allowNoOpOnly = true;
        [SerializeField] private bool rejectRequestsWithResourceFlags = true;

        [Header("ProgrammedAction Routing")]
        [SerializeField] private bool allowProgrammedActionPreparedOnly = true;
        [SerializeField] private bool allowProgrammedActionResourceFlagsPreparedOnly = true;

        [Header("BodyAction Limited Execution")]
        [Tooltip("only selected body actions are delegated to BodyActionExecutor. This is not the full body runtime route yet.")]
        [SerializeField] private bool executeReturnCenterBodyAction = true;
        [SerializeField] private bool executeIdleNodBodyAction = true;
        [SerializeField] private bool executeLightSearchBodyAction = true;
        [SerializeField] private bool executeLookAroundBodyAction = true;
        [SerializeField] private bool executeSearchUserBodyAction = true;
        [Tooltip("allow fixed speech rules to route crawler_stop / emergency_stop to BodyActionExecutor. Safety stops are still executed below BodyActionExecutor, not here.")]
        [SerializeField] private bool executeCrawlerStopBodyAction = true;
        [Tooltip("allow fixed speech rules to route crawler_forward_short to BodyActionExecutor. Keep this OFF if testing stop-only first.")]
        [SerializeField] private bool executeCrawlerForwardShortBodyAction = true;
        [Tooltip("allow fixed speech rules to route crawler_back_short to BodyActionExecutor.")]
        [SerializeField] private bool executeCrawlerBackShortBodyAction = true;
        [Tooltip("allow fixed speech rules to route crawler_turn_left_short to BodyActionExecutor.")]
        [SerializeField] private bool executeCrawlerTurnLeftShortBodyAction = true;
        [Tooltip("allow fixed speech rules to route crawler_turn_right_short to BodyActionExecutor.")]
        [SerializeField] private bool executeCrawlerTurnRightShortBodyAction = true;
        [SerializeField] private bool requireFullyLostForSearchUser = true;
        [SerializeField] private FacePerceptionBuffer facePerceptionBuffer;
        [SerializeField] private global::BodyActionExecutor bodyActionExecutor;

        [Header("SafeState Request")]
        [SerializeField] private bool observeSafeStateRequests = true;

        [Header("Interruptible Action Slot")]
        [SerializeField] private bool observeInterruptibleActionSlot = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        public bool IsRunning { get; private set; }
        public ExecutionRequest CurrentRequest { get; private set; }
        public ExecutionStatus CurrentStatus { get; private set; } = ExecutionStatus.None;

        public IInterruptibleAction CurrentInterruptibleAction { get; private set; }
        public string CurrentInterruptibleActionName { get; private set; } = string.Empty;
        public bool HasInterruptibleAction
        {
            get { return CurrentInterruptibleAction != null; }
        }

        public event Action<ExecutionRequest> ExecutionStarted;
        public event Action<ExecutionResult> ExecutionFinished;

        /// <summary>
        /// Read-only legacy body runtime evidence. Observers must not use
        /// this event to start, stop, or otherwise control body execution.
        /// </summary>
        public event Action<BodyExecutionRuntimeFact>
            BodyLifecycleObserved;

        private int bodyRuntimeGeneration;
        private string bodyRuntimeToken = string.Empty;
        private string bodyRuntimeActionId = string.Empty;
        private bool bodyRuntimeTerminal;

        public bool CanStartExecution()
        {
            if (IsRunning)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ExecutionController] CanStartExecution=false reason=already running"
                    );
                }

                return false;
            }

            return true;
        }

        public bool TryStartExecution(ExecutionRequest request)
        {
            string rejectReason;
            if (!ValidateRequest(request, out rejectReason))
            {
                RejectRequest(request, rejectReason);
                return false;
            }

            if (!CanStartExecution())
            {
                RejectRequest(request, "ExecutionController is already running");
                return false;
            }

            CurrentRequest = request;
            CurrentStatus = ExecutionStatus.Running;
            IsRunning = true;

            if (IsBodyRuntimeRequest(request))
            {
                BeginBodyRuntimeObservation(request);
                EmitBodyRuntimeFact(
                    BodyRuntimeLifecycle.RequestAccepted,
                    new ExecutionDomainStage(
                        ExecutionDomainStageIds.RequestAccepted),
                    ExecutionTerminalOutcome.None,
                    BodyPhysicalVerificationLevel.None,
                    string.Empty,
                    "ExecutionController accepted legacy body request");
            }

            if (verboseLog)
            {
                Debug.Log("[ExecutionController] Start execution: " + request);

                if (request.ActionId != AllowedNoOpActionId)
                {
                    Debug.Log(
                        "[ExecutionController] ProgrammedAction route accepted. " +
                        "role=execution-entry " +
                        "bodyLifecycle=delegated-to-BodyActionExecutor-when-enabled " +
                        "action=" +
                        request.ActionId +
                        " requiresBody=" +
                        request.RequiresBody +
                        " requiresSpeech=" +
                        request.RequiresSpeech +
                        " requiresExpression=" +
                        request.RequiresExpression +
                        " requiresDevice=" +
                        request.RequiresDevice +
                        " mode=" + (ShouldExecuteLimitedBodyAction(request) ? "body-action" : "prepared-only") +
                        " route=" + (ShouldExecuteLimitedBodyAction(request) ? "body-action-executor" : "no-body-action")
                    );
                }
            }

            ExecutionStarted?.Invoke(request);

            if (ShouldExecuteLimitedBodyAction(request))
            {
                ExecuteLimitedBodyAction(request);
                return true;
            }

            StartCoroutine(CompletePreparedOnlyOnNextFrame(request));
            return true;
        }

        private bool ShouldExecuteLimitedBodyAction(ExecutionRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (executeReturnCenterBodyAction && request.ActionId == ActionReturnCenter)
            {
                return true;
            }

            if (executeIdleNodBodyAction && request.ActionId == ActionIdleNod)
            {
                return true;
            }

            if (executeLightSearchBodyAction && request.ActionId == ActionLightSearch)
            {
                return true;
            }

            if (executeLookAroundBodyAction && request.ActionId == ActionLookAround)
            {
                return true;
            }

            if (executeSearchUserBodyAction && request.ActionId == ActionSearchUser)
            {
                return CanExecuteSearchUserBodyAction();
            }

            if (executeCrawlerStopBodyAction &&
                (request.ActionId == ActionCrawlerStop || request.ActionId == ActionEmergencyStop))
            {
                return true;
            }

            if (executeCrawlerForwardShortBodyAction && request.ActionId == ActionCrawlerForwardShort)
            {
                return true;
            }

            if (executeCrawlerBackShortBodyAction && request.ActionId == ActionCrawlerBackShort)
            {
                return true;
            }

            if (executeCrawlerTurnLeftShortBodyAction && request.ActionId == ActionCrawlerTurnLeftShort)
            {
                return true;
            }

            if (executeCrawlerTurnRightShortBodyAction && request.ActionId == ActionCrawlerTurnRightShort)
            {
                return true;
            }

            return false;
        }

        private bool CanExecuteSearchUserBodyAction()
        {
            if (!requireFullyLostForSearchUser)
            {
                return true;
            }

            if (facePerceptionBuffer == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ExecutionController] searchUser body route skipped. " +
                        "reason=FacePerceptionBuffer is not assigned requireFullyLostForSearchUser=true"
                    );
                }

                return false;
            }

            if (facePerceptionBuffer.IsFullyLost)
            {
                return true;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionController] searchUser body route skipped. " +
                    "reason=face-state-not-fully-lost state=" +
                    facePerceptionBuffer.State +
                    " noFaceDuration=" +
                    facePerceptionBuffer.NoFaceDuration.ToString("0.00") +
                    " route=prepared-only"
                );
            }

            return false;
        }

        private void ExecuteLimitedBodyAction(ExecutionRequest request)
        {
            if (bodyActionExecutor == null)
            {
                Debug.LogWarning(
                    "[ExecutionController] BodyAction route failed: " +
                    "role=execution-entry route=body-action-executor " +
                    "BodyActionExecutor is not assigned. action=" +
                    SafeLogValue(request != null ? request.ActionId : string.Empty)
                );

                FinishInternal(
                    ExecutionResult.Failed(
                        request,
                        "BodyActionExecutor is not assigned"
                    )
                );
                return;
            }

            bool executed = false;

            try
            {
                executed = bodyActionExecutor.TryExecuteProgrammedAction(
                    request.ActionId,
                    request.Reason,
                    "ExecutionController"
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[ExecutionController] BodyAction execution exception: " +
                    "action=" +
                    SafeLogValue(request != null ? request.ActionId : string.Empty) +
                    " message=" +
                    ex.Message
                );

                FinishInternal(
                    ExecutionResult.Failed(
                        request,
                        "BodyAction execution exception: " + ex.Message
                    )
                );
                return;
            }

            if (!executed)
            {
                Debug.LogWarning(
                    "[ExecutionController] BodyAction execution failed: " +
                    "role=execution-entry route=body-action-executor " +
                    "action=" +
                    SafeLogValue(request != null ? request.ActionId : string.Empty)
                );

                FinishInternal(
                    ExecutionResult.Failed(
                        request,
                        "BodyActionExecutor returned false"
                    )
                );
                return;
            }

            Debug.Log(
                "[ExecutionController] BodyAction execution requested. " +
                "role=execution-entry route=body-action-executor " +
                "note=BodyActionExecutor currently owns Acting/Recovering and part-specific execution. " +
                "action=" +
                SafeLogValue(request.ActionId) +
                " route=body-action-executor"
            );

            EmitBodyRuntimeFact(
                BodyRuntimeLifecycle.PhysicalDispatchRequested,
                new ExecutionDomainStage(
                    ExecutionDomainStageIds.PhysicalDispatched),
                ExecutionTerminalOutcome.None,
                BodyPhysicalVerificationLevel.None,
                string.Empty,
                "BodyActionExecutor accepted execution request; " +
                "transport write and physical completion are not confirmed");

            FinishInternal(
                ExecutionResult.Completed(
                    request,
                    "BodyAction execution requested"
                )
            );
        }

        private bool ValidateRequest(ExecutionRequest request, out string reason)
        {
            reason = string.Empty;

            if (request == null)
            {
                reason = "request is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ActionId))
            {
                reason = "ActionId is empty";
                return false;
            }

            bool isNoOp = request.ActionId == AllowedNoOpActionId;
            bool isProgrammedActionPreparedOnly =
                allowProgrammedActionPreparedOnly &&
                IsPreparedOnlyProgrammedAction(request.ActionId);

            if (allowNoOpOnly && !isNoOp && !isProgrammedActionPreparedOnly)
            {
                reason =
                    "Phase 9-R allows action=none or registered ProgrammedAction. action=" +
                    request.ActionId;
                return false;
            }

            if (rejectRequestsWithResourceFlags &&
                HasRuntimeResourceRequest(request) &&
                !(isProgrammedActionPreparedOnly && allowProgrammedActionResourceFlagsPreparedOnly))
            {
                reason =
                    "Phase 6-C rejects resource execution flags. " +
                    "requiresBody=" + request.RequiresBody +
                    " requiresSpeech=" + request.RequiresSpeech +
                    " requiresExpression=" + request.RequiresExpression +
                    " requiresDevice=" + request.RequiresDevice;
                return false;
            }

            return true;
        }

        private static bool HasRuntimeResourceRequest(ExecutionRequest request)
        {
            if (request == null)
            {
                return false;
            }

            return
                request.RequiresBody ||
                request.RequiresSpeech ||
                request.RequiresExpression ||
                request.RequiresDevice;
        }

        private void RejectRequest(ExecutionRequest request, string reason)
        {
            string safeReason = reason ?? string.Empty;

            Debug.LogWarning("[ExecutionController] Reject execution: " + safeReason);

            if (request != null)
            {
                if (IsBodyRuntimeRequest(request))
                    BeginBodyRuntimeObservation(request);
                FinishInternal(ExecutionResult.Rejected(request, safeReason));
            }
            else
            {
                CurrentStatus = ExecutionStatus.Rejected;
            }
        }

        /// <summary>
        /// Prepared-only completion route.
        ///
        /// This is used when ExecutionController accepts a request for logging / runtime
        /// lifecycle confirmation, but does not delegate to a real output executor.
        /// No body, voice, expression, device, or state-changing action is performed here.
        /// </summary>
        private IEnumerator CompletePreparedOnlyOnNextFrame(ExecutionRequest request)
        {
            yield return null;

            if (CurrentRequest != request)
            {
                yield break;
            }

            FinishInternal(ExecutionResult.Completed(request, "ExecutionController prepared-only completed"));
        }


        /// <summary>
        /// Phase 7-C:
        /// RuntimeProcessor から SafeStateRequest を受け取る入口。
        ///
        /// この段階では observe-only。
        /// ここでは以下を行わない。
        /// - CancelCurrent() を呼ばない
        /// - IsRunning / CurrentRequest を変更しない
        /// - BodyActionExecutor / VoiceController / Servo を止めない
        /// - InteractionState を変更しない
        ///
        /// 目的は、InterruptArbiter -> SafeStateRequest -> ExecutionController
        /// までの安全な経路をログで確認すること。
        /// </summary>
        public bool TryRequestSafeState(SafeStateRequest request)
        {
            if (!observeSafeStateRequests)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[ExecutionController] SafeState request ignored: observeSafeStateRequests=false"
                    );
                }

                return false;
            }

            if (request == null)
            {
                Debug.LogWarning(
                    "[ExecutionController] SafeState request rejected: request is null"
                );
                return false;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionController] SafeState requested: " +
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
                    (request.HasStateController ? request.CurrentState.ToString() : "Unknown") +
                    " canInterrupt=" +
                    (request.HasLimboPermission ? request.CanInterrupt.ToString() : "Unknown") +
                    " emergency=" +
                    (request.HasLimboPermission ? request.IsEmergencyMode.ToString() : "Unknown") +
                    " isRunning=" +
                    IsRunning +
                    " currentAction=" +
                    (CurrentRequest != null ? SafeLogValue(CurrentRequest.ActionId) : "none") +
                    " hasInterruptibleAction=" +
                    HasInterruptibleAction +
                    " interruptibleAction=" +
                    SafeLogValue(CurrentInterruptibleActionName) +
                    " interruptibleCanSafeState=" +
                    GetInterruptibleCanEnterSafeStateLogValue() +
                    " route=observe-only/no-cancel/no-state-change/no-action-interrupt " +
                    "message=" +
                    SafeLogValue(request.Message)
                );
            }

            return true;
        }

        /// <summary>
        /// Phase 7-D:
        /// 将来、実行中の BodyAction / SpeechAction / DeviceAction などを
        /// SafeState へ移行させるための保持スロット。
        ///
        /// この段階では登録・解除・ログのみ。
        /// TryRequestSafeState() から RequestSafeState() / CancelAction() は呼ばない。
        /// </summary>
        public void RegisterInterruptibleAction(IInterruptibleAction action, string actionName = "")
        {
            CurrentInterruptibleAction = action;
            CurrentInterruptibleActionName = string.IsNullOrWhiteSpace(actionName)
                ? (action != null ? action.GetType().Name : string.Empty)
                : actionName;

            if (verboseLog && observeInterruptibleActionSlot)
            {
                Debug.Log(
                    "[ExecutionController] InterruptibleAction registered: " +
                    "name=" +
                    SafeLogValue(CurrentInterruptibleActionName) +
                    " hasAction=" +
                    HasInterruptibleAction +
                    " canEnterSafeState=" +
                    GetInterruptibleCanEnterSafeStateLogValue() +
                    " route=slot-only/no-action-interrupt"
                );
            }
        }

        /// <summary>
        /// Phase 7-D:
        /// InterruptibleAction保持スロットを解除する。
        /// この段階では実Action側から呼ばれる前提ではなく、将来接続用の入口だけを用意する。
        /// </summary>
        public void ClearInterruptibleAction(IInterruptibleAction action = null, string reason = "")
        {
            if (action != null && CurrentInterruptibleAction != action)
            {
                if (verboseLog && observeInterruptibleActionSlot)
                {
                    Debug.Log(
                        "[ExecutionController] InterruptibleAction clear ignored: reason=action mismatch " +
                        "requested=" + action.GetType().Name +
                        " current=" + SafeLogValue(CurrentInterruptibleActionName) +
                        " message=" + SafeLogValue(reason)
                    );
                }

                return;
            }

            string previousName = CurrentInterruptibleActionName;
            CurrentInterruptibleAction = null;
            CurrentInterruptibleActionName = string.Empty;

            if (verboseLog && observeInterruptibleActionSlot)
            {
                Debug.Log(
                    "[ExecutionController] InterruptibleAction cleared: " +
                    "previous=" +
                    SafeLogValue(previousName) +
                    " reason=" +
                    SafeLogValue(reason) +
                    " route=slot-only/no-action-interrupt"
                );
            }
        }

        private string GetInterruptibleCanEnterSafeStateLogValue()
        {
            if (CurrentInterruptibleAction == null)
            {
                return "False";
            }

            try
            {
                return CurrentInterruptibleAction.CanEnterSafeState.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[ExecutionController] InterruptibleAction CanEnterSafeState read failed: " +
                    ex.Message
                );
                return "Unknown";
            }
        }


        public void CancelCurrent(string reason = "")
        {
            if (!IsRunning || CurrentRequest == null)
            {
                if (verboseLog)
                {
                    Debug.Log("[ExecutionController] Cancel ignored: no running execution");
                }

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[ExecutionController] Cancel current execution: action=" +
                    CurrentRequest.ActionId +
                    " reason=" +
                    reason
                );
            }

            FinishInternal(ExecutionResult.Cancelled(CurrentRequest, reason));
        }


        private static string SafeLogValue(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value;
        }

        private void FinishInternal(ExecutionResult result)
        {
            if (result == null)
            {
                return;
            }

            CurrentStatus = result.Status;

            EmitTerminalBodyRuntimeFact(result);

            if (verboseLog)
            {
                Debug.Log("[ExecutionController] Finish execution: " + result);
            }

            IsRunning = false;
            CurrentRequest = null;

            ExecutionFinished?.Invoke(result);
        }

        private static bool IsBodyRuntimeRequest(ExecutionRequest request)
        {
            return request != null &&
                (request.RequiresBody ||
                 (!string.IsNullOrWhiteSpace(request.ActionId) &&
                  request.ActionId != AllowedNoOpActionId));
        }

        private void BeginBodyRuntimeObservation(ExecutionRequest request)
        {
            bodyRuntimeGeneration = bodyRuntimeGeneration == int.MaxValue
                ? 1 : bodyRuntimeGeneration + 1;
            bodyRuntimeToken =
                "execution-controller:" + GetInstanceID() + ":" +
                bodyRuntimeGeneration;
            bodyRuntimeActionId = request != null
                ? request.ActionId ?? string.Empty : string.Empty;
            bodyRuntimeTerminal = false;
        }

        private void EmitTerminalBodyRuntimeFact(ExecutionResult result)
        {
            if (result == null || result.Status == ExecutionStatus.Completed)
                return;

            if (!string.IsNullOrEmpty(bodyRuntimeToken) &&
                !string.Equals(
                    bodyRuntimeActionId,
                    result.ActionId ?? string.Empty,
                    StringComparison.Ordinal))
                return;

            if (string.IsNullOrEmpty(bodyRuntimeToken))
            {
                ExecutionRequest request = CurrentRequest;
                if (request == null || !IsBodyRuntimeRequest(request))
                    return;
                BeginBodyRuntimeObservation(request);
            }

            BodyRuntimeLifecycle lifecycle;
            ExecutionTerminalOutcome outcome;
            switch (result.Status)
            {
                case ExecutionStatus.Rejected:
                    lifecycle = BodyRuntimeLifecycle.RequestRejected;
                    outcome = ExecutionTerminalOutcome.Rejected;
                    break;
                case ExecutionStatus.Cancelled:
                    lifecycle = BodyRuntimeLifecycle.Interrupted;
                    outcome = ExecutionTerminalOutcome.Interrupted;
                    break;
                case ExecutionStatus.Failed:
                    lifecycle = BodyRuntimeLifecycle.Failed;
                    outcome = ExecutionTerminalOutcome.Failed;
                    break;
                default:
                    return;
            }

            EmitBodyRuntimeFact(
                lifecycle, null, outcome,
                BodyPhysicalVerificationLevel.None,
                result.Message, "ExecutionController terminal result");
        }

        private void EmitBodyRuntimeFact(
            BodyRuntimeLifecycle lifecycle,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            BodyPhysicalVerificationLevel verificationLevel,
            string failureReason,
            string diagnostic)
        {
            if (string.IsNullOrEmpty(bodyRuntimeToken) ||
                bodyRuntimeTerminal)
                return;

            bool terminal =
                lifecycle == BodyRuntimeLifecycle.RequestRejected ||
                lifecycle == BodyRuntimeLifecycle.Failed ||
                lifecycle == BodyRuntimeLifecycle.Interrupted ||
                lifecycle == BodyRuntimeLifecycle.SafetyPreempted;

            if (terminal)
                bodyRuntimeTerminal = true;

            BodyExecutionRuntimeFact fact =
                new BodyExecutionRuntimeFact(
                    bodyRuntimeToken, bodyRuntimeGeneration,
                    bodyRuntimeActionId, lifecycle, domainStage,
                    terminalOutcome, verificationLevel,
                    DateTime.UtcNow, failureReason, diagnostic);

            Delegate[] listeners =
                BodyLifecycleObserved?.GetInvocationList();
            if (listeners == null)
                return;

            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action<BodyExecutionRuntimeFact>)listeners[i])
                        .Invoke(fact);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[ExecutionController] " +
                        "BodyLifecycleObserved exception: " + exception);
                }
            }
        }
    }
}
