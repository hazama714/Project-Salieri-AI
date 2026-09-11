// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections;
using SalieriAI.Core.Action;
using SalieriAI.Core.Affordance;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Limbo;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.State;
using SalieriAI.Expression.Voice;
using UnityEngine;

public sealed class BodyActionExecutor : MonoBehaviour
{
    private const string ActionNone = "none";
    private const string ActionLookAround = "lookAround";
    private const string ActionReturnCenter = "returnCenter";
    private const string ActionIdleNod = "idleNod";
    private const string ActionLightSearch = "lightSearch";
    private const string ActionSearchUser = "searchUser";
    private const string ActionSpeakShort = "speakShort";

    private const string ActionCrawlerStop = "crawler_stop";
    private const string ActionCrawlerForwardShort = "crawler_forward_short";
    private const string ActionCrawlerBackShort = "crawler_back_short";
    private const string ActionCrawlerTurnLeftShort = "crawler_turn_left_short";
    private const string ActionCrawlerTurnRightShort = "crawler_turn_right_short";

    private const string ActionBodyStop = "body_stop";
    private const string ActionAllStop = "all_stop";
    private const string ActionEmergencyStop = "emergency_stop";

    [Header("References")]
    [SerializeField] private NeckActionExecutor neckActionExecutor;
    [SerializeField] private VoiceController voiceController;
    [SerializeField] private CrawlerActionExecutor crawlerActionExecutor;

    [Header("State / Limbo")]
    [SerializeField] private LimboPermission limboPermission;
    [SerializeField] private InteractionStateController stateController;
    [SerializeField] private RobotConditionCollector robotConditionCollector;

    [Header("Canonical Orientation Action")]
    [SerializeField] private float actionOverrideSeconds = 2.0f;

    [Header("Crawler")]
    [SerializeField] private float crawlerRecoverDelaySeconds = 0.6f;

    [Header("Voice")]
    [SerializeField] private string defaultSpeakerName = "冥鳴ひまり";
    [SerializeField] private string defaultShortSpeech = "少し考えていました。";
    private Coroutine neckActionRecoverCoroutine;
    private Coroutine crawlerRecoverCoroutine;

    private InteractionState stateBeforeAction = InteractionState.Idle;
    private int safetyRuntimeGeneration;

    /// <summary>
    /// Read-only evidence from the current safety action boundary. The
    /// current executor can prove observation/acceptance only; it cannot
    /// prove transport dispatch or physical stop.
    /// </summary>
    public event Action<SafetyControlRuntimeFact> SafetyLifecycleObserved;

    private void Awake()
    {
        if (limboPermission == null)
            limboPermission = FindObjectOfType<LimboPermission>();

        if (stateController == null)
            stateController = FindObjectOfType<InteractionStateController>();

        if (robotConditionCollector == null)
            robotConditionCollector = FindObjectOfType<RobotConditionCollector>();

        if (neckActionExecutor == null)
            neckActionExecutor = FindObjectOfType<NeckActionExecutor>();

        if (crawlerActionExecutor == null)
            crawlerActionExecutor = FindObjectOfType<CrawlerActionExecutor>();
    }

    public void Execute(LLMActionDecision decision)
    {
        if (decision == null)
        {
            Debug.LogWarning("[BodyActionExecutor] decision is null");
            return;
        }

        decision.Normalize();

        string action = NormalizeActionName(decision.action);
        TryExecuteNormalizedAction(action, decision.reason, "LLMActionDecision");
    }

    public ActionResult Execute(AffordanceCandidate candidate)
    {
        if (candidate == null)
            return ActionResult.Failed("null", "AffordanceCandidate is null.");

        Debug.Log(
            $"[BodyActionExecutor] affordance={candidate.Type} " +
            $"actionId={candidate.ActionId} reason={candidate.Reason}"
        );

        string action = NormalizeActionName(candidate.ActionId);

        if (action == ActionNone)
            return ActionResult.Ok(ActionNone, "No action executed.");

        bool executed = TryExecuteNormalizedAction(action, candidate.Reason, "AffordanceCandidate");

        if (!executed)
            return ActionResult.Failed(action, "Action was not executed.");

        return ActionResult.Ok(action, "Action execution requested.");
    }

    public bool TryExecuteProgrammedAction(
        string actionId,
        string reason = "",
        string source = "ProgrammedAction"
    )
    {
        string action = NormalizeActionName(actionId);

        Debug.Log(
            $"[BodyActionExecutor] TryExecuteProgrammedAction action={action} " +
            $"source={source} reason={reason}"
        );

        return TryExecuteNormalizedAction(action, reason, source);
    }

    private bool TryExecuteNormalizedAction(string action, string reason, string source)
    {
        action = NormalizeActionName(action);
        if (reason == null)
            reason = string.Empty;

        if (source == null)
            source = "unknown";

        Debug.Log($"[BodyActionExecutor] action={action} source={source} reason={reason}");

        if (action == ActionNone)
            return true;

        if (IsSafetyStopAction(action))
        {
            Debug.Log($"[BodyActionExecutor] SafetyStop action={action} bypass CanStartAction");
            int generation = ++safetyRuntimeGeneration;
            string token = "body-action-safety:" + GetInstanceID() + ":" + generation;
            ExecutionControlType kind = SafetyControlKind(action);
            EmitSafetyFact(token, generation, kind,
                SafetyRuntimeLifecycle.RequestObserved, action,
                SafetyRuntimeVerificationLevel.RequestOnly, "", source);
            EmitSafetyFact(token, generation, kind,
                SafetyRuntimeLifecycle.RequestAccepted, action,
                SafetyRuntimeVerificationLevel.RequestOnly, "", source);
            try
            {
                bool executed = ExecuteSafetyStopAction(action, source, reason);
                if (!executed)
                    EmitSafetyFact(token, generation, kind,
                        SafetyRuntimeLifecycle.Failed, action,
                        SafetyRuntimeVerificationLevel.RequestOnly,
                        "SAFETY_RUNTIME_REQUEST_FAILED", source);
                return executed;
            }
            catch (Exception exception)
            {
                EmitSafetyFact(token, generation, kind,
                    SafetyRuntimeLifecycle.Failed, action,
                    SafetyRuntimeVerificationLevel.RequestOnly,
                    "SAFETY_RUNTIME_EXCEPTION:" +
                    exception.GetType().Name, source);
                throw;
            }
        }

        if (!CanStartActionNow(action, source, reason))
            return false;

        robotConditionCollector?.NotifyAction(action);
        Debug.Log($"[BodyActionExecutor] NotifyAction action={action}");

        EnterActingState();

        try
        {
            switch (action)
            {
                case ActionLookAround:
                case ActionReturnCenter:
                case ActionIdleNod:
                case ActionLightSearch:
                case ActionSearchUser:
                    return ExecuteNeckAction(action, source, reason);

                case ActionCrawlerForwardShort:
                case ActionCrawlerBackShort:
                case ActionCrawlerTurnLeftShort:
                case ActionCrawlerTurnRightShort:
                    return ExecuteCrawlerAction(action, source, reason);

                case ActionSpeakShort:
                    SpeakShortPlaceholder();
                    return true;

                default:
                    Debug.LogWarning($"[BodyActionExecutor] unknown action: {action}");
                    FinishRecoveringImmediately($"unknown action={action}");
                    return false;
            }
        }
        catch
        {
            FinishRecoveringImmediately($"exception action={action}");
            throw;
        }
    }

    private static string NormalizeActionName(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return ActionNone;

        action = action.Trim();

        switch (action)
        {
            case "none":
                return ActionNone;

            case "lookAround":
            case "search_front":
            case "search":
                return ActionLookAround;

            case "lightSearch":
            case "light_search":
                return ActionLightSearch;

            case "searchUser":
            case "search_user":
            case "findUser":
            case "find_user":
                return ActionSearchUser;

            case "returnCenter":
            case "look_front":
            case "look_at_target":
            case "hold_position":
                return ActionReturnCenter;

            case "idleNod":
            case "nod":
                return ActionIdleNod;

            case "speakShort":
            case "speak_short":
            case "speak":
                return ActionSpeakShort;

            case "crawler_stop":
            case "crawlerStop":
            case "stopCrawler":
                return ActionCrawlerStop;

            case "crawler_forward_short":
            case "crawlerForwardShort":
            case "crawler_fwd_short":
            case "move_forward_short":
                return ActionCrawlerForwardShort;

            case "crawler_back_short":
            case "crawlerBackShort":
            case "crawler_backward_short":
            case "move_back_short":
                return ActionCrawlerBackShort;

            case "crawler_turn_left_short":
            case "crawlerTurnLeftShort":
            case "turn_left_short":
                return ActionCrawlerTurnLeftShort;

            case "crawler_turn_right_short":
            case "crawlerTurnRightShort":
            case "turn_right_short":
                return ActionCrawlerTurnRightShort;

            case "body_stop":
            case "bodyStop":
                return ActionBodyStop;

            case "all_stop":
            case "allStop":
            case "stop_all":
                return ActionAllStop;

            case "emergency_stop":
            case "emergencyStop":
            case "absolute_stop":
            case "zettai_stop":
                return ActionEmergencyStop;

            default:
                return action;
        }
    }

    private static bool IsSafetyStopAction(string action)
    {
        switch (action)
        {
            case ActionCrawlerStop:
            case ActionBodyStop:
            case ActionAllStop:
            case ActionEmergencyStop:
                return true;

            default:
                return false;
        }
    }

    private static ExecutionControlType SafetyControlKind(string action)
    {
        if (action == ActionEmergencyStop)
            return ExecutionControlType.EmergencyPreempt;
        if (action == ActionCrawlerStop)
            return ExecutionControlType.Stop;
        return ExecutionControlType.SafetyStop;
    }

    private void EmitSafetyFact(
        string token, int generation, ExecutionControlType kind,
        SafetyRuntimeLifecycle lifecycle, string targetId,
        SafetyRuntimeVerificationLevel verification,
        string failureReason, string diagnostic)
    {
        Action<SafetyControlRuntimeFact> handler = SafetyLifecycleObserved;
        if (handler == null) return;
        try
        {
            handler(new SafetyControlRuntimeFact(
                token, generation, kind,
                SafetyRuntimeActualScope.CrawlerOnly,
                lifecycle, targetId, DateTime.UtcNow,
                failureReason, verification, diagnostic));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[BodyActionExecutor] Safety lifecycle observer failed: " +
                exception.Message, this);
        }
    }

    private bool ExecuteSafetyStopAction(string action, string source, string reason)
    {
        Debug.Log($"[BodyActionExecutor] ExecuteSafetyStopAction action={action} source={source} reason={reason}");

        switch (action)
        {
            case ActionCrawlerStop:
                return ExecuteCrawlerAction(ActionCrawlerStop, source, reason);

            case ActionBodyStop:
            case ActionAllStop:
            case ActionEmergencyStop:
                // 現段階でBody停止として確実に送れるのはキャタピラ停止。
                // 将来ここに neck_stop / servo_stop_all / voice_stop / tracking_stop を追加する。
                return ExecuteCrawlerAction(ActionCrawlerStop, source, reason);

            default:
                Debug.LogWarning($"[BodyActionExecutor] unknown safety stop action: {action}");
                return false;
        }
    }

    private bool ExecuteCrawlerAction(string action, string source, string reason)
    {
        if (crawlerActionExecutor == null)
        {
            Debug.LogWarning($"[BodyActionExecutor] crawlerActionExecutor is null action={action}");

            if (stateController != null &&
                stateController.CurrentState == InteractionState.Acting)
            {
                FinishRecoveringImmediately(
                    $"crawlerActionExecutor is null action={action}"
                );
            }

            return false;
        }
        // CrawlerActionExecutor.TryExecute signature is (action, reason, source).
        bool executed = crawlerActionExecutor.TryExecute(action, reason, source);

        if (!executed)
        {
            Debug.LogWarning($"[BodyActionExecutor] crawler action failed action={action}");

            if (stateController != null &&
                stateController.CurrentState == InteractionState.Acting)
            {
                FinishRecoveringImmediately(
                    $"crawler action failed action={action}"
                );
            }

            return false;
        }
        ScheduleCrawlerRecovering();
        return true;
    }

    private void ScheduleCrawlerRecovering()
    {
        if (crawlerRecoverCoroutine != null)
            StopCoroutine(crawlerRecoverCoroutine);

        crawlerRecoverCoroutine = StartCoroutine(CrawlerRecoveringRoutine());
    }

    private IEnumerator CrawlerRecoveringRoutine()
    {
        Debug.Log($"[BodyActionExecutor] Crawler action recovering delay={crawlerRecoverDelaySeconds:0.00}s");

        if (stateController != null && stateController.CurrentState == InteractionState.Acting)
            ExitActingState();

        if (crawlerRecoverDelaySeconds > 0f)
            yield return new WaitForSeconds(crawlerRecoverDelaySeconds);

        crawlerRecoverCoroutine = null;

        FinishRecovering();
    }

    private bool ExecuteNeckAction(string action, string source, string reason)
    {
        if (neckActionExecutor == null)
        {
            Debug.LogWarning(
                $"[BodyActionExecutor] neckActionExecutor is null action={action}"
            );

            FinishRecoveringImmediately(
                $"neckActionExecutor is null action={action}"
            );

            return false;
        }

        bool executed = neckActionExecutor.TryExecute(
            action,
            source,
            reason,
            actionOverrideSeconds);

        if (!executed)
        {
            FinishRecoveringImmediately(
                $"neck action failed action={action}"
            );

            return false;
        }

        ScheduleNeckActionRecovering();
        return true;
    }

    private bool CanStartActionNow(string actionName, string source, string reason)
    {
        if (limboPermission == null)
            return true;

        if (limboPermission.IsEmergencyMode)
        {
            Debug.Log(
                $"[BodyActionExecutor] Blocked by Limbo Emergency " +
                $"action={actionName} source={source} reason={reason}"
            );
            return false;
        }

        if (limboPermission.CanStartAction)
            return true;

        if (CanStartExecutionControllerLimitedBodyAction(actionName, source))
        {
            InteractionState currentState =
                stateController != null ? stateController.CurrentState : InteractionState.Idle;

            Debug.Log(
                $"[BodyActionExecutor] Limited BodyAction allowed before Acting despite CanStartAction=false. " +
                $"action={actionName} source={source} state={currentState} reason={reason}"
            );
            return true;
        }

        Debug.Log(
            $"[BodyActionExecutor] Blocked by Limbo CanStartAction=false " +
            $"action={actionName} source={source} reason={reason} " +
            $"state={(stateController != null ? stateController.CurrentState.ToString() : "<no-state-controller>")}"
        );
        return false;
    }

    private bool CanStartExecutionControllerLimitedBodyAction(string actionName, string source)
    {
        if (!IsExecutionControllerSource(source))
            return false;

        if (stateController == null)
            return false;

        InteractionState currentState = stateController.CurrentState;
        if (currentState != InteractionState.Idle &&
            currentState != InteractionState.Listening)
        {
            return false;
        }

        // Phase 10-I-2:
        // 自発探索 lookAround は STT / Listening への影響を避けるため、
        // ExecutionController 経由であっても Idle のときだけ許可する。
        // Crawler系の音声BodyActionは従来通り Listening でも限定許可する。
        if (actionName == ActionLookAround &&
            currentState != InteractionState.Idle)
        {
            return false;
        }

        return IsLimitedBodyActionAllowedFromExecutionController(actionName);
    }

    private static bool IsExecutionControllerSource(string source)
    {
        return string.Equals(source, "ExecutionController", System.StringComparison.Ordinal);
    }

    private static bool IsLimitedBodyActionAllowedFromExecutionController(string actionName)
    {
        switch (actionName)
        {
            case ActionReturnCenter:
            case ActionIdleNod:
            case ActionLookAround:
            case ActionCrawlerForwardShort:
            case ActionCrawlerBackShort:
            case ActionCrawlerTurnLeftShort:
            case ActionCrawlerTurnRightShort:
                return true;

            default:
                return false;
        }
    }

    private bool CanMoveServoNow(string actionName)
    {
        if (limboPermission == null)
            return true;

        if (limboPermission.IsEmergencyMode)
        {
            Debug.Log($"[BodyActionExecutor] Servo blocked by Emergency action={actionName}");
            ExitActingState();
            return false;
        }

        if (!limboPermission.CanMoveServo)
        {
            Debug.Log($"[BodyActionExecutor] Servo blocked by Limbo action={actionName}");
            ExitActingState();
            return false;
        }

        return true;
    }

    private bool CanSpeakNow()
    {
        if (limboPermission == null)
            return true;

        if (limboPermission.IsEmergencyMode)
        {
            Debug.Log("[BodyActionExecutor] Speak blocked by Emergency");
            return false;
        }

        if (!limboPermission.CanSpeak)
        {
            Debug.Log("[BodyActionExecutor] Speak blocked by Limbo");
            return false;
        }

        return true;
    }

    private void SpeakShortPlaceholder()
    {
        // Phase 10-H: Voice actions are intentionally not connected from BodyActionExecutor yet.
        // Keep speakShort as a recognized no-op action so Action routing can be tested
        // without mixing VoiceController responsibilities into BodyActionExecutor.
        if (!CanSpeakNow())
        {
            FinishRecoveringImmediately(
                "speakShort blocked"
            );

            return;
        }

        Debug.Log(
            $"[BodyActionExecutor] speakShort placeholder. " +
            $"VoiceActionExecutor is not connected yet. " +
            $"speaker={defaultSpeakerName} text={defaultShortSpeech}"
        );

        FinishRecoveringImmediately(
            "speakShort placeholder completed"
        );
    }

    private void EnterActingState()
    {
        if (stateController == null)
            return;

        stateBeforeAction = stateController.CurrentState;
        stateController.SetActing();
    }

    private void ExitActingState()
    {
        if (stateController == null)
            return;

        if (stateController.CurrentState == InteractionState.Emergency)
            return;

        stateController.SetRecovering();
    }

    private void FinishRecoveringImmediately(string reason)
    {
        Debug.Log(
            $"[BodyActionExecutor] FinishRecoveringImmediately reason={reason}"
        );

        ExitActingState();
        FinishRecovering();
    }

    private void FinishRecovering()
    {
        if (stateController == null)
            return;

        if (stateController.CurrentState == InteractionState.Emergency)
            return;

        if (stateBeforeAction == InteractionState.Tracking ||
            stateBeforeAction == InteractionState.TemporaryLost ||
            stateBeforeAction == InteractionState.FullyLost ||
            stateBeforeAction == InteractionState.Searching)
        {
            stateController.SetState(stateBeforeAction);
            return;
        }

        stateController.SetIdle();
    }

    private void ScheduleNeckActionRecovering()
    {
        if (neckActionRecoverCoroutine != null)
            StopCoroutine(neckActionRecoverCoroutine);

        neckActionRecoverCoroutine = StartCoroutine(
            NeckActionRecoveringRoutine()
        );
    }

    private IEnumerator NeckActionRecoveringRoutine()
    {
        Debug.Log(
            $"[BodyActionExecutor] Canonical orientation request active " +
            $"{actionOverrideSeconds}s");

        yield return new WaitForSeconds(actionOverrideSeconds);

        neckActionRecoverCoroutine = null;

        FinishRecovering();
    }
}
