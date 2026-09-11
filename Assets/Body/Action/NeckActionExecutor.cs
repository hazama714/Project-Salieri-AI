// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Perception.Attention;
using SalieriAI.Core.Perception.ObjectTargeting;
using SalieriAI.Core.Perception.ObjectTracking;

using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Historical class name retained for Scene/API compatibility.
///
/// Phase 4 responsibility: translate an existing neck ActionId into an
/// Orientation target request. It does not calculate physical neck angles,
/// reference NeckController, or write AttentionTarget directly.
/// </summary>
public sealed class NeckActionExecutor : MonoBehaviour
{
    private const string ActionLookAround = "lookAround";
    private const string ActionReturnCenter = "returnCenter";
    private const string ActionIdleNod = "idleNod";
    private const string ActionLightSearch = "lightSearch";
    private const string ActionSearchUser = "searchUser";

    private const string RequestSourceKey = "neck-action-orientation";

    [Header("Canonical Orientation Request")]
    [SerializeField]
    private OrientationPriorityRequestService priorityRequestService;

    [SerializeField]
    [Range(0, 100)]
    private int requestPriority = 80;

    [SerializeField]
    [Min(0.05f)]
    private float defaultRequestDurationSeconds = 2f;

    [Header("Body-relative Gaze Target (not physical angles)")]
    [FormerlySerializedAs("lookAroundYaw")]
    [SerializeField] private float lookAroundTargetYaw = 25f;

    [FormerlySerializedAs("lightSearchYaw")]
    [SerializeField] private float lightSearchTargetYaw = 15f;

    [FormerlySerializedAs("searchUserYaw")]
    [SerializeField] private float searchUserTargetYaw = 25f;

    [FormerlySerializedAs("nodPitch")]
    [SerializeField] private float nodTargetPitch = -10f;

    [Header("Closed-loop lookAround")]
    [SerializeField]
    [Min(0.1f)]
    private float lookAroundStepYawDegrees = 5f;

    [Header("Runtime State (Read Only)")]
    [SerializeField] private string lastActionId = string.Empty;
    [SerializeField] private string lastRequestId = string.Empty;
    [SerializeField] private string lastTargetKey = string.Empty;

    private ByteTrackObjectTrackingService objectTrackingService;
    private ObservationTargetSelectionService objectTargetSelectionService;
    private OrientationResolutionTargetDriver resolutionTargetDriver;
    private readonly SearchClosedLoopMotionGate lookAroundMotionGate =
        new SearchClosedLoopMotionGate();
    private string pendingObservationSessionId = string.Empty;
    private long pendingObservationSourceFrameId;
    private bool observationSubscribed;

    public string LastActionId => lastActionId;
    public string LastRequestId => lastRequestId;
    public string LastTargetKey => lastTargetKey;
    public bool IsLookAroundSearchActive => lookAroundMotionGate.IsActive;
    public int LookAroundSearchStepCount => lookAroundMotionGate.StepCount;
    public float LookAroundStepYawDegrees => lookAroundStepYawDegrees;

    private void Awake()
    {
        if (priorityRequestService == null)
            priorityRequestService =
                FindObjectOfType<OrientationPriorityRequestService>();

        ResolveSearchDependencies();
    }

    private void OnEnable()
    {
        ResolveSearchDependencies();
        SubscribeSearchObservation();
    }

    private void OnDisable()
    {
        UnsubscribeSearchObservation();
        InterruptLookAroundSearch("NeckActionExecutor disabled");
    }

    private void OnDestroy()
    {
        UnsubscribeSearchObservation();
    }

    public void Bind(
        OrientationPriorityRequestService service)
    {
        if (service != null)
            priorityRequestService = service;
    }

    public static bool IsNeckAction(string action)
    {
        switch (action)
        {
            case ActionLookAround:
            case ActionReturnCenter:
            case ActionIdleNod:
            case ActionLightSearch:
            case ActionSearchUser:
                return true;

            default:
                return false;
        }
    }

    public bool TryExecute(
        string action,
        string source = "BodyActionExecutor",
        string reason = "",
        float durationSeconds = -1f)
    {
        if (priorityRequestService == null)
        {
            Debug.LogWarning(
                "[NeckActionExecutor] OrientationPriorityRequestService " +
                "is not assigned. action=" + action,
                this);
            return false;
        }

        float duration = durationSeconds > 0f
            ? durationSeconds
            : defaultRequestDurationSeconds;

        if (!string.Equals(
                action,
                ActionLookAround,
                StringComparison.Ordinal))
        {
            InterruptLookAroundSearch(
                "Superseded by neck action " + (action ?? string.Empty));
        }

        switch (action)
        {
            case ActionLookAround:
                return StartLookAroundSearch(
                    source, reason, duration);

            case ActionLightSearch:
                return SubmitTargetDirection(
                    action, lightSearchTargetYaw, 0f,
                    source, reason, duration);

            case ActionSearchUser:
                return SubmitTargetDirection(
                    action, searchUserTargetYaw, 0f,
                    source, reason, duration);

            case ActionIdleNod:
                return SubmitTargetDirection(
                    action, 0f, nodTargetPitch,
                    source, reason, duration);

            case ActionReturnCenter:
                return SubmitNeutral(
                    action, source, reason, duration);

            default:
                Debug.LogWarning(
                    "[NeckActionExecutor] unknown neck action=" + action,
                    this);
                return false;
        }
    }

    public bool InterruptLookAroundSearch(string reason)
    {
        SearchClosedLoopMotionDecision decision =
            lookAroundMotionGate.Interrupt(reason);
        if (decision != SearchClosedLoopMotionDecision.Interrupted)
            return false;

        pendingObservationSessionId = string.Empty;
        pendingObservationSourceFrameId = 0L;
        priorityRequestService?.RemoveBySourceKey(RequestSourceKey);

        Debug.Log(
            "[SearchMotion][INTERRUPTED] Reason=" +
            (reason ?? string.Empty),
            this);
        return true;
    }

    private bool StartLookAroundSearch(
        string source,
        string reason,
        float durationSeconds)
    {
        InterruptLookAroundSearch("Replaced by new lookAround request");
        ResolveSearchDependencies();
        SubscribeSearchObservation();

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long durationMilliseconds = Math.Max(
            1L,
            (long)Math.Round(durationSeconds * 1000.0));

        string baselineSessionId = string.Empty;
        long baselineSourceFrameId = 0L;
        if (objectTrackingService != null &&
            objectTrackingService.LatestSnapshot != null)
        {
            baselineSessionId =
                objectTrackingService.LatestSnapshot.SessionId ??
                string.Empty;
            baselineSourceFrameId =
                objectTrackingService.LatestSnapshot.SourceFrameId;
        }

        GetCurrentBodyDirectionAngles(
            out float currentYaw,
            out float currentPitch);

        SearchClosedLoopMotionDecision decision =
            lookAroundMotionGate.Begin(
                baselineSessionId,
                baselineSourceFrameId,
                currentYaw,
                currentPitch,
                lookAroundTargetYaw,
                lookAroundStepYawDegrees,
                now,
                now + durationMilliseconds,
                out float requestedYaw,
                out float requestedPitch,
                out string decisionReason);

        if (decision == SearchClosedLoopMotionDecision.None)
        {
            Debug.LogWarning(
                "[SearchMotion][START_REJECTED] Reason=" + decisionReason,
                this);
            return false;
        }

        if (!SubmitLookAroundDirection(
                requestedYaw,
                requestedPitch,
                source,
                reason,
                durationSeconds))
        {
            lookAroundMotionGate.Interrupt("Priority request rejected");
            return false;
        }

        LogSearchStep(decision, baselineSourceFrameId, decisionReason);
        Debug.Log(
            "[SearchMotion][WAIT] Reason=AwaitFreshObservation " +
            "ObservationRevision=" + baselineSourceFrameId,
            this);
        return true;
    }

    private void LateUpdate()
    {
        if (!lookAroundMotionGate.IsActive)
            return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SearchClosedLoopMotionDecision deadlineDecision =
            lookAroundMotionGate.CheckDeadline(now, out string deadlineReason);
        if (deadlineDecision == SearchClosedLoopMotionDecision.TimedOut)
        {
            CompleteLookAroundSearch("TIMEOUT", deadlineReason);
            return;
        }

        if (pendingObservationSourceFrameId <= 0L)
            return;

        string sessionId = pendingObservationSessionId;
        long sourceFrameId = pendingObservationSourceFrameId;
        pendingObservationSessionId = string.Empty;
        pendingObservationSourceFrameId = 0L;

        ObservationTarget selected =
            objectTargetSelectionService != null
                ? objectTargetSelectionService.CurrentTarget
                : null;
        bool targetFound =
            selected != null &&
            selected.SourceFrameId == sourceFrameId &&
            string.Equals(
                selected.SessionId,
                sessionId,
                StringComparison.Ordinal);

        SearchClosedLoopMotionDecision decision =
            lookAroundMotionGate.Observe(
                sessionId,
                sourceFrameId,
                targetFound,
                now,
                out float requestedYaw,
                out float requestedPitch,
                out string decisionReason);

        if (decision ==
            SearchClosedLoopMotionDecision.AwaitFreshObservationHold)
        {
            return;
        }

        Debug.Log(
            "[SearchMotion][REOBSERVED] ObservationRevision=" +
            sourceFrameId +
            " TargetFound=" + targetFound,
            this);

        if (decision == SearchClosedLoopMotionDecision.TargetFound)
        {
            string targetKey = selected != null
                ? selected.TargetKey
                : string.Empty;
            priorityRequestService?.RemoveBySourceKey(RequestSourceKey);
            lookAroundMotionGate.CompleteTargetHandoff();
            Debug.Log(
                "[SearchMotion][TARGET_FOUND] Target=" + targetKey +
                " SearchCompleted=true",
                this);
            return;
        }

        if (decision == SearchClosedLoopMotionDecision.TimedOut)
        {
            CompleteLookAroundSearch("TIMEOUT", decisionReason);
            return;
        }

        if (decision != SearchClosedLoopMotionDecision.SearchStepRequested &&
            decision != SearchClosedLoopMotionDecision.MaximumExtentHold)
        {
            return;
        }

        double remainingMilliseconds = Math.Max(
            1.0,
            lookAroundMotionGate.DeadlineUnixMilliseconds - now);
        float remainingSeconds =
            (float)(remainingMilliseconds / 1000.0);

        if (!SubmitLookAroundDirection(
                requestedYaw,
                requestedPitch,
                "SearchClosedLoopReobservation",
                decisionReason,
                remainingSeconds))
        {
            InterruptLookAroundSearch("Search step request rejected");
            return;
        }

        LogSearchStep(decision, sourceFrameId, decisionReason);
        Debug.Log(
            "[SearchMotion][WAIT] Reason=AwaitFreshObservation " +
            "ObservationRevision=" + sourceFrameId,
            this);
    }

    private bool SubmitLookAroundDirection(
        float yaw,
        float pitch,
        string source,
        string reason,
        float durationSeconds)
    {
        return SubmitTargetDirection(
            ActionLookAround,
            yaw,
            pitch,
            source,
            reason,
            durationSeconds);
    }

    private void HandleTracksUpdated(TrackedObjectSet snapshot)
    {
        if (!lookAroundMotionGate.IsActive ||
            snapshot == null ||
            snapshot.SourceFrameId <= 0L)
        {
            return;
        }

        pendingObservationSessionId = snapshot.SessionId ?? string.Empty;
        pendingObservationSourceFrameId = snapshot.SourceFrameId;
    }

    private void CompleteLookAroundSearch(
        string outcome,
        string reason)
    {
        pendingObservationSessionId = string.Empty;
        pendingObservationSourceFrameId = 0L;
        priorityRequestService?.RemoveBySourceKey(RequestSourceKey);
        Debug.Log(
            "[SearchMotion][" + outcome + "] Reason=" +
            (reason ?? string.Empty),
            this);
    }

    private void ResolveSearchDependencies()
    {
        if (objectTrackingService == null)
            objectTrackingService =
                FindObjectOfType<ByteTrackObjectTrackingService>();

        if (objectTargetSelectionService == null)
            objectTargetSelectionService =
                FindObjectOfType<ObservationTargetSelectionService>();

        if (resolutionTargetDriver == null)
            resolutionTargetDriver =
                FindObjectOfType<OrientationResolutionTargetDriver>();
    }

    private void SubscribeSearchObservation()
    {
        if (observationSubscribed || objectTrackingService == null)
            return;

        objectTrackingService.TracksUpdated += HandleTracksUpdated;
        observationSubscribed = true;
    }

    private void UnsubscribeSearchObservation()
    {
        if (!observationSubscribed || objectTrackingService == null)
            return;

        objectTrackingService.TracksUpdated -= HandleTracksUpdated;
        observationSubscribed = false;
    }

    private void GetCurrentBodyDirectionAngles(
        out float yaw,
        out float pitch)
    {
        yaw = 0f;
        pitch = 0f;

        if (resolutionTargetDriver == null)
            return;

        resolutionTargetDriver.TryGetCurrentBodyDirectionAngles(
            out yaw,
            out pitch);
    }

    private void LogSearchStep(
        SearchClosedLoopMotionDecision decision,
        long observationRevision,
        string reason)
    {
        Debug.Log(
            "[SearchMotion][STEP] Step=" +
            lookAroundMotionGate.StepCount +
            " Direction=Right" +
            " Yaw=" + lookAroundMotionGate.CurrentYawDegrees.ToString("0.###") +
            " MaxStep=" + lookAroundStepYawDegrees.ToString("0.###") +
            " ObservationRevision=" + observationRevision +
            " Decision=" + decision +
            " Reason=" + (reason ?? string.Empty),
            this);
    }

    private bool SubmitTargetDirection(
        string action,
        float yaw,
        float pitch,
        string source,
        string reason,
        float durationSeconds)
    {
        string targetKey = "orientation-action:" + action;
        var target = new OrientationTargetDirectionRequest(
            targetKey,
            yaw,
            pitch);

        return Submit(
            action,
            OrientationPriorityDirective.TargetDirection,
            target,
            source,
            reason,
            durationSeconds);
    }

    private bool SubmitNeutral(
        string action,
        string source,
        string reason,
        float durationSeconds)
    {
        return Submit(
            action,
            OrientationPriorityDirective.Neutral,
            null,
            source,
            reason,
            durationSeconds);
    }

    private bool Submit(
        string action,
        OrientationPriorityDirective directive,
        OrientationTargetDirectionRequest target,
        string source,
        string reason,
        float durationSeconds)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long durationMilliseconds = Math.Max(
            1L,
            (long)Math.Round(durationSeconds * 1000.0));
        string requestId = Guid.NewGuid().ToString("N");

        var request = new OrientationPriorityRequest(
            requestId,
            RequestSourceKey,
            directive,
            OrientationPriorityReason.Default,
            requestPriority,
            now,
            now + durationMilliseconds,
            target);

        if (!priorityRequestService.SubmitOrReplace(
                request,
                out string error))
        {
            Debug.LogWarning(
                "[NeckActionExecutor] target request rejected " +
                "action=" + action + " error=" + error,
                this);
            return false;
        }

        lastActionId = action ?? string.Empty;
        lastRequestId = requestId;
        lastTargetKey = target != null
            ? target.TargetKey
            : "neutral";

        Debug.Log(
            "[NeckActionExecutor][CANONICAL_TARGET_REQUESTED] " +
            "action=" + action +
            " directive=" + directive +
            " target=" + lastTargetKey +
            " source=" + (source ?? string.Empty) +
            " reason=" + (reason ?? string.Empty),
            this);
        return true;
    }
}
