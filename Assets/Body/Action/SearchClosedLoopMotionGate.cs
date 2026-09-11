// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

/// <summary>
/// Search motion lifecycle owned by the body action boundary.
/// This is not object identity and does not move a Transform or a servo.
/// </summary>
public enum SearchClosedLoopMotionState
{
    Idle = 0,
    AwaitingFreshObservation = 1,
    TargetFound = 2,
    Completed = 3,
    Interrupted = 4
}

public enum SearchClosedLoopMotionDecision
{
    None = 0,
    SearchStepRequested = 1,
    AwaitFreshObservationHold = 2,
    MaximumExtentHold = 3,
    TargetFound = 4,
    TimedOut = 5,
    Interrupted = 6
}

/// <summary>
/// Pure one-step-per-fresh-perception gate for the existing lookAround
/// direction policy. SessionId + SourceFrameId is a runtime observation
/// revision only; it is never promoted to persistent identity.
/// </summary>
public sealed class SearchClosedLoopMotionGate
{
    private const float Epsilon = 0.0001f;

    private string lastObservationSessionId = string.Empty;
    private long lastObservationSourceFrameId;
    private float currentYawDegrees;
    private float heldPitchDegrees;
    private float finalYawDegrees;
    private float maximumStepDegrees;
    private long deadlineUnixMilliseconds;
    private int stepCount;

    public SearchClosedLoopMotionState State { get; private set; }
        = SearchClosedLoopMotionState.Idle;

    public bool IsActive =>
        State == SearchClosedLoopMotionState.AwaitingFreshObservation;

    public int StepCount => stepCount;
    public float CurrentYawDegrees => currentYawDegrees;
    public float HeldPitchDegrees => heldPitchDegrees;
    public float FinalYawDegrees => finalYawDegrees;
    public long DeadlineUnixMilliseconds => deadlineUnixMilliseconds;
    public string LastObservationSessionId => lastObservationSessionId;
    public long LastObservationSourceFrameId =>
        lastObservationSourceFrameId;

    public SearchClosedLoopMotionDecision Begin(
        string baselineSessionId,
        long baselineSourceFrameId,
        float currentYaw,
        float currentPitch,
        float targetYaw,
        float stepDegrees,
        long nowUnixMilliseconds,
        long deadlineMilliseconds,
        out float requestedYaw,
        out float requestedPitch,
        out string reason)
    {
        requestedYaw = currentYaw;
        requestedPitch = currentPitch;
        reason = string.Empty;

        if (!IsFinite(currentYaw) ||
            !IsFinite(currentPitch) ||
            !IsFinite(targetYaw) ||
            !IsFinite(stepDegrees) ||
            stepDegrees <= 0f ||
            nowUnixMilliseconds <= 0L ||
            deadlineMilliseconds <= nowUnixMilliseconds)
        {
            Reset();
            reason = "Invalid search start contract.";
            return SearchClosedLoopMotionDecision.None;
        }

        lastObservationSessionId =
            baselineSessionId == null
                ? string.Empty
                : baselineSessionId;
        lastObservationSourceFrameId =
            baselineSourceFrameId > 0L
                ? baselineSourceFrameId
                : 0L;
        currentYawDegrees = currentYaw;
        heldPitchDegrees = currentPitch;
        finalYawDegrees = targetYaw;
        maximumStepDegrees = stepDegrees;
        deadlineUnixMilliseconds = deadlineMilliseconds;
        stepCount = 0;
        State = SearchClosedLoopMotionState.AwaitingFreshObservation;

        return RequestNextStep(
            out requestedYaw,
            out requestedPitch,
            out reason);
    }

    public SearchClosedLoopMotionDecision Observe(
        string sessionId,
        long sourceFrameId,
        bool targetFound,
        long nowUnixMilliseconds,
        out float requestedYaw,
        out float requestedPitch,
        out string reason)
    {
        requestedYaw = currentYawDegrees;
        requestedPitch = heldPitchDegrees;
        reason = string.Empty;

        if (!IsActive)
        {
            reason = "Search is not active.";
            return SearchClosedLoopMotionDecision.None;
        }

        if (nowUnixMilliseconds >= deadlineUnixMilliseconds)
        {
            State = SearchClosedLoopMotionState.Completed;
            reason = "Search deadline reached.";
            return SearchClosedLoopMotionDecision.TimedOut;
        }

        if (!IsFreshObservation(sessionId, sourceFrameId))
        {
            reason = "Awaiting a fresh perception revision.";
            return SearchClosedLoopMotionDecision
                .AwaitFreshObservationHold;
        }

        lastObservationSessionId = sessionId;
        lastObservationSourceFrameId = sourceFrameId;

        if (targetFound)
        {
            State = SearchClosedLoopMotionState.TargetFound;
            reason = "Fresh perception selected an Object target.";
            return SearchClosedLoopMotionDecision.TargetFound;
        }

        return RequestNextStep(
            out requestedYaw,
            out requestedPitch,
            out reason);
    }

    public SearchClosedLoopMotionDecision CheckDeadline(
        long nowUnixMilliseconds,
        out string reason)
    {
        reason = string.Empty;

        if (!IsActive ||
            nowUnixMilliseconds < deadlineUnixMilliseconds)
        {
            return SearchClosedLoopMotionDecision.None;
        }

        State = SearchClosedLoopMotionState.Completed;
        reason = "Search deadline reached.";
        return SearchClosedLoopMotionDecision.TimedOut;
    }

    public SearchClosedLoopMotionDecision Interrupt(
        string reason)
    {
        if (!IsActive)
            return SearchClosedLoopMotionDecision.None;

        State = SearchClosedLoopMotionState.Interrupted;
        return SearchClosedLoopMotionDecision.Interrupted;
    }

    public void CompleteTargetHandoff()
    {
        if (State == SearchClosedLoopMotionState.TargetFound)
            State = SearchClosedLoopMotionState.Completed;
    }

    public void Reset()
    {
        lastObservationSessionId = string.Empty;
        lastObservationSourceFrameId = 0L;
        currentYawDegrees = 0f;
        heldPitchDegrees = 0f;
        finalYawDegrees = 0f;
        maximumStepDegrees = 0f;
        deadlineUnixMilliseconds = 0L;
        stepCount = 0;
        State = SearchClosedLoopMotionState.Idle;
    }

    private SearchClosedLoopMotionDecision RequestNextStep(
        out float requestedYaw,
        out float requestedPitch,
        out string reason)
    {
        requestedPitch = heldPitchDegrees;

        float remaining = finalYawDegrees - currentYawDegrees;
        if (Math.Abs(remaining) <= Epsilon)
        {
            requestedYaw = currentYawDegrees;
            reason = "Existing search maximum extent reached; holding.";
            return SearchClosedLoopMotionDecision.MaximumExtentHold;
        }

        float magnitude = Math.Min(
            Math.Abs(remaining),
            maximumStepDegrees);
        currentYawDegrees += Math.Sign(remaining) * magnitude;
        requestedYaw = currentYawDegrees;
        stepCount++;
        reason = "One bounded Search step admitted.";
        return SearchClosedLoopMotionDecision.SearchStepRequested;
    }

    private bool IsFreshObservation(
        string sessionId,
        long sourceFrameId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            sourceFrameId <= 0L)
        {
            return false;
        }

        if (!string.Equals(
                sessionId,
                lastObservationSessionId,
                StringComparison.Ordinal))
        {
            return true;
        }

        return sourceFrameId > lastObservationSourceFrameId;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
