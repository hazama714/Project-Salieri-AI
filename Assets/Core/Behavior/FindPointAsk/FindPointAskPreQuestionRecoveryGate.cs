// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Behavior.FindPointAsk
{
    public enum FindPointAskRecoveryStage
    {
        None = 0,
        Look = 10,
        Point = 20
    }

    public enum FindPointAskPreQuestionRecoveryState
    {
        Idle = 0,
        Searching = 10,
        Reacquired = 20,
        TimedOut = 30,
        Interrupted = 40
    }

    public enum FindPointAskPreQuestionRecoveryDecision
    {
        None = 0,
        SearchStarted = 10,
        AwaitFreshObservation = 20,
        DifferentTargetRejected = 30,
        SameTargetReacquired = 40,
        TimedOut = 50,
        Interrupted = 60
    }

    /// <summary>
    /// Pure lifecycle/identity gate for pre-question recovery. It does not
    /// implement search motion. Production search remains owned by the
    /// existing NeckActionExecutor/SearchClosedLoopMotionGate path.
    /// </summary>
    public sealed class FindPointAskPreQuestionRecoveryGate
    {
        private string targetKey = string.Empty;
        private string sessionId = string.Empty;
        private int trackId;
        private long baselineSourceFrameId;
        private long lastEvaluatedSourceFrameId;
        private double deadlineSeconds;

        public FindPointAskPreQuestionRecoveryState State { get; private set; }
            = FindPointAskPreQuestionRecoveryState.Idle;
        public FindPointAskRecoveryStage Stage { get; private set; }
            = FindPointAskRecoveryStage.None;
        public string TargetKey => targetKey;
        public string SessionId => sessionId;
        public int TrackId => trackId;
        public long BaselineSourceFrameId => baselineSourceFrameId;
        public long LastEvaluatedSourceFrameId => lastEvaluatedSourceFrameId;
        public double DeadlineSeconds => deadlineSeconds;
        public bool IsActive =>
            State == FindPointAskPreQuestionRecoveryState.Searching;
        public bool RequiresFreshLookRestart =>
            State == FindPointAskPreQuestionRecoveryState.Reacquired;

        public static bool RequiresRecoveryFromResolverState(
            bool wasResolvedByResolver,
            bool resolverHasFrozenTarget,
            bool selectionHasFrozenTarget)
        {
            return (wasResolvedByResolver && !resolverHasFrozenTarget) ||
                (!resolverHasFrozenTarget && !selectionHasFrozenTarget);
        }

        public FindPointAskPreQuestionRecoveryDecision Begin(
            FindPointAskRecoveryStage lostStage,
            string frozenTargetKey,
            int frozenTrackId,
            string frozenSessionId,
            long lostObservationSourceFrameId,
            double nowSeconds,
            double timeoutSeconds,
            out string reason)
        {
            reason = string.Empty;
            if ((lostStage != FindPointAskRecoveryStage.Look &&
                 lostStage != FindPointAskRecoveryStage.Point) ||
                string.IsNullOrWhiteSpace(frozenTargetKey) ||
                string.IsNullOrWhiteSpace(frozenSessionId) ||
                frozenTrackId < 0 ||
                lostObservationSourceFrameId < 0L ||
                double.IsNaN(nowSeconds) ||
                double.IsInfinity(nowSeconds) ||
                double.IsNaN(timeoutSeconds) ||
                double.IsInfinity(timeoutSeconds) ||
                timeoutSeconds <= 0.0)
            {
                Reset();
                reason = "Invalid pre-question recovery contract.";
                return FindPointAskPreQuestionRecoveryDecision.None;
            }

            Stage = lostStage;
            targetKey = frozenTargetKey.Trim();
            sessionId = frozenSessionId.Trim();
            trackId = frozenTrackId;
            baselineSourceFrameId = lostObservationSourceFrameId;
            lastEvaluatedSourceFrameId = lostObservationSourceFrameId;
            deadlineSeconds = nowSeconds + timeoutSeconds;
            State = FindPointAskPreQuestionRecoveryState.Searching;
            reason = "Existing closed-loop Search may start.";
            return FindPointAskPreQuestionRecoveryDecision.SearchStarted;
        }

        public FindPointAskPreQuestionRecoveryDecision Observe(
            bool hasTarget,
            string observedTargetKey,
            int observedTrackId,
            string observedSessionId,
            long observedSourceFrameId,
            double nowSeconds,
            out string reason)
        {
            reason = string.Empty;
            FindPointAskPreQuestionRecoveryDecision deadline =
                CheckDeadline(nowSeconds, out reason);
            if (deadline == FindPointAskPreQuestionRecoveryDecision.TimedOut)
                return deadline;

            if (!IsActive)
            {
                reason = "Recovery is not active.";
                return FindPointAskPreQuestionRecoveryDecision.None;
            }

            if (!hasTarget ||
                string.IsNullOrWhiteSpace(observedTargetKey) ||
                string.IsNullOrWhiteSpace(observedSessionId) ||
                observedSourceFrameId <= lastEvaluatedSourceFrameId)
            {
                reason = "Awaiting a fresh selected-target observation.";
                return FindPointAskPreQuestionRecoveryDecision
                    .AwaitFreshObservation;
            }

            lastEvaluatedSourceFrameId = observedSourceFrameId;
            bool same = string.Equals(
                    targetKey,
                    observedTargetKey.Trim(),
                    StringComparison.Ordinal) &&
                trackId == observedTrackId &&
                string.Equals(
                    sessionId,
                    observedSessionId.Trim(),
                    StringComparison.Ordinal);
            if (!same)
            {
                reason = "Fresh target does not match frozen short-term identity.";
                return FindPointAskPreQuestionRecoveryDecision
                    .DifferentTargetRejected;
            }

            State = FindPointAskPreQuestionRecoveryState.Reacquired;
            reason = "Frozen target was reacquired on a fresh observation.";
            return FindPointAskPreQuestionRecoveryDecision
                .SameTargetReacquired;
        }

        public FindPointAskPreQuestionRecoveryDecision CheckDeadline(
            double nowSeconds,
            out string reason)
        {
            reason = string.Empty;
            if (!IsActive || nowSeconds < deadlineSeconds)
                return FindPointAskPreQuestionRecoveryDecision.None;

            State = FindPointAskPreQuestionRecoveryState.TimedOut;
            reason = "Pre-question recovery deadline reached.";
            return FindPointAskPreQuestionRecoveryDecision.TimedOut;
        }

        public FindPointAskPreQuestionRecoveryDecision Interrupt(
            string reason)
        {
            if (!IsActive)
                return FindPointAskPreQuestionRecoveryDecision.None;

            State = FindPointAskPreQuestionRecoveryState.Interrupted;
            return FindPointAskPreQuestionRecoveryDecision.Interrupted;
        }

        public void Reset()
        {
            targetKey = string.Empty;
            sessionId = string.Empty;
            trackId = 0;
            baselineSourceFrameId = 0L;
            lastEvaluatedSourceFrameId = 0L;
            deadlineSeconds = 0.0;
            Stage = FindPointAskRecoveryStage.None;
            State = FindPointAskPreQuestionRecoveryState.Idle;
        }
    }
}
