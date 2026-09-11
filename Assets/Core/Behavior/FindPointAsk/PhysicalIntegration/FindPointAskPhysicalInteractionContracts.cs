// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Behavior.FindPointAsk.PhysicalIntegration
{
    public enum FindPointAskPhysicalCompletionState
    {
        NotObserved = 0,
        PhysicalCompletionUnverified = 10
    }

    public enum FindPointAskPhysicalPoseOwnershipState
    {
        None = 0,
        Owned = 10,
        Released = 20
    }

    public enum FindPointAskPhysicalPoseReleaseReason
    {
        None = 0,
        QuestionSpeechCompleted = 10,
        AnswerReceived = 20,
        KnownSuppressed = 30,
        PreQuestionRecovery = 35,
        Failed = 40,
        Cancelled = 50,
        Disabled = 60,
        QuestionTimedOut = 70,
        Interrupted = 80,
        Emergency = 90,
        ExplicitNeutral = 100
    }

    public enum FindPointAskTargetLostOutcome
    {
        None = 0,
        FailedBeforeHold = 10,
        FrozenTargetContinuedAfterHold = 20
    }

    /// <summary>
    /// One interaction run's immutable provenance. These values correlate a
    /// transient physical gesture; none is promoted to persistent entity
    /// identity.
    /// </summary>
    public sealed class FindPointAskPhysicalRunTarget
    {
        public string BehaviorRunId { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public string SessionId { get; }
        public long SourceFrameId { get; }
        public DateTime FrozenAtUtc { get; }

        public bool IsValid =>
            BehaviorRunId.Length > 0 &&
            TargetKey.Length > 0 &&
            SessionId.Length > 0 &&
            SourceFrameId > 0;

        public FindPointAskPhysicalRunTarget(
            string behaviorRunId,
            string targetKey,
            int trackId,
            string sessionId,
            long sourceFrameId,
            DateTime frozenAtUtc)
        {
            BehaviorRunId = Normalize(behaviorRunId);
            TargetKey = Normalize(targetKey);
            TrackId = trackId;
            SessionId = Normalize(sessionId);
            SourceFrameId = sourceFrameId;
            FrozenAtUtc = frozenAtUtc.Kind == DateTimeKind.Utc
                ? frozenAtUtc
                : frozenAtUtc.ToUniversalTime();
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    /// <summary>
    /// Read-only snapshot of M6 causal evidence. A dispatched command never
    /// changes PhysicalPositionReachedVerified: current hardware has no such
    /// feedback contract.
    /// </summary>
    public sealed class FindPointAskPhysicalInteractionState
    {
        public FindPointAskPhysicalRunTarget RunTarget { get; }
        public bool KnownSuppressed { get; }
        public bool LookRequested { get; }
        public bool LookLogicalConfirmed { get; }
        public string LookTargetKey { get; }
        public bool NeckCommandDispatched { get; }
        public int NeckServoId { get; }
        public int NeckServoAngle { get; }
        public FindPointAskPhysicalCompletionState NeckCompletion { get; }
        public bool PointRequested { get; }
        public bool PointLogicalConfirmed { get; }
        public string PointTargetKey { get; }
        public string PointArmId { get; }
        public float PointTargetWorldX { get; }
        public float PointTargetWorldY { get; }
        public float PointTargetWorldZ { get; }
        public bool ArmCommandDispatched { get; }
        public int ArmServoId { get; }
        public int ArmServoAngle { get; }
        public FindPointAskPhysicalCompletionState ArmCompletion { get; }
        public bool AskRequested { get; }
        public string AskTargetKey { get; }
        public string QuestionId { get; }
        public string QuestionTargetKey { get; }
        public string RuntimeSpeechId { get; }
        public bool SpeechRequested { get; }
        public bool SpeechStarted { get; }
        public bool SpeechCompleted { get; }
        public bool WaitAnswerRetentionActive { get; }
        public FindPointAskPhysicalPoseOwnershipState PoseOwnership { get; }
        public FindPointAskPhysicalPoseReleaseReason PoseReleaseReason { get; }
        public int PoseReleaseCount { get; }
        public FindPointAskTargetLostOutcome TargetLostOutcome { get; }

        public bool PhysicalPositionReachedVerified => false;

        internal FindPointAskPhysicalInteractionState(
            FindPointAskPhysicalInteractionTracker source)
        {
            RunTarget = source.RunTarget;
            KnownSuppressed = source.KnownSuppressed;
            LookRequested = source.LookRequested;
            LookLogicalConfirmed = source.LookLogicalConfirmed;
            LookTargetKey = source.LookTargetKey;
            NeckCommandDispatched = source.NeckCommandDispatched;
            NeckServoId = source.NeckServoId;
            NeckServoAngle = source.NeckServoAngle;
            NeckCompletion = source.NeckCompletion;
            PointRequested = source.PointRequested;
            PointLogicalConfirmed = source.PointLogicalConfirmed;
            PointTargetKey = source.PointTargetKey;
            PointArmId = source.PointArmId;
            PointTargetWorldX = source.PointTargetWorldX;
            PointTargetWorldY = source.PointTargetWorldY;
            PointTargetWorldZ = source.PointTargetWorldZ;
            ArmCommandDispatched = source.ArmCommandDispatched;
            ArmServoId = source.ArmServoId;
            ArmServoAngle = source.ArmServoAngle;
            ArmCompletion = source.ArmCompletion;
            AskRequested = source.AskRequested;
            AskTargetKey = source.AskTargetKey;
            QuestionId = source.QuestionId;
            QuestionTargetKey = source.QuestionTargetKey;
            RuntimeSpeechId = source.RuntimeSpeechId;
            SpeechRequested = source.SpeechRequested;
            SpeechStarted = source.SpeechStarted;
            SpeechCompleted = source.SpeechCompleted;
            WaitAnswerRetentionActive = source.WaitAnswerRetentionActive;
            PoseOwnership = source.PoseOwnership;
            PoseReleaseReason = source.PoseReleaseReason;
            PoseReleaseCount = source.PoseReleaseCount;
            TargetLostOutcome = source.TargetLostOutcome;
        }
    }

    /// <summary>
    /// Pure causal tracker. It owns no Transform, hardware, storage, clock,
    /// target selection, speech execution, or servo authority.
    /// </summary>
    public sealed class FindPointAskPhysicalInteractionTracker
    {
        public FindPointAskPhysicalRunTarget RunTarget { get; }
        internal bool KnownSuppressed { get; private set; }
        internal bool LookRequested { get; private set; }
        internal bool LookLogicalConfirmed { get; private set; }
        internal string LookTargetKey { get; private set; } = string.Empty;
        internal bool NeckCommandDispatched { get; private set; }
        internal int NeckServoId { get; private set; } = -1;
        internal int NeckServoAngle { get; private set; }
        internal FindPointAskPhysicalCompletionState NeckCompletion { get; private set; }
        internal bool PointRequested { get; private set; }
        internal bool PointLogicalConfirmed { get; private set; }
        internal string PointTargetKey { get; private set; } = string.Empty;
        internal string PointArmId { get; private set; } = string.Empty;
        internal float PointTargetWorldX { get; private set; }
        internal float PointTargetWorldY { get; private set; }
        internal float PointTargetWorldZ { get; private set; }
        internal bool ArmCommandDispatched { get; private set; }
        internal int ArmServoId { get; private set; } = -1;
        internal int ArmServoAngle { get; private set; }
        internal FindPointAskPhysicalCompletionState ArmCompletion { get; private set; }
        internal bool AskRequested { get; private set; }
        internal string AskTargetKey { get; private set; } = string.Empty;
        internal string QuestionId { get; private set; } = string.Empty;
        internal string QuestionTargetKey { get; private set; } = string.Empty;
        internal string RuntimeSpeechId { get; private set; } = string.Empty;
        internal bool SpeechRequested { get; private set; }
        internal bool SpeechStarted { get; private set; }
        internal bool SpeechCompleted { get; private set; }
        internal bool WaitAnswerRetentionActive { get; private set; }
        internal FindPointAskPhysicalPoseOwnershipState PoseOwnership { get; private set; }
        internal FindPointAskPhysicalPoseReleaseReason PoseReleaseReason { get; private set; }
        internal int PoseReleaseCount { get; private set; }
        internal FindPointAskTargetLostOutcome TargetLostOutcome { get; private set; }

        public FindPointAskPhysicalInteractionState CurrentState =>
            new FindPointAskPhysicalInteractionState(this);

        public FindPointAskPhysicalInteractionTracker(
            FindPointAskPhysicalRunTarget runTarget)
        {
            RunTarget = runTarget ?? throw new ArgumentNullException(nameof(runTarget));
            if (!runTarget.IsValid)
                throw new ArgumentException("M6 run target is incomplete.", nameof(runTarget));
        }

        public bool TryRecordKnownSuppressed(string targetKey)
        {
            if (!Matches(targetKey) || LookRequested || PointRequested || AskRequested)
                return false;
            KnownSuppressed = true;
            return TryReleasePose(FindPointAskPhysicalPoseReleaseReason.KnownSuppressed);
        }

        public bool TryRecordLookRequested(string targetKey)
        {
            if (KnownSuppressed || LookRequested || !Matches(targetKey))
                return false;
            LookTargetKey = Normalize(targetKey);
            LookRequested = true;
            PoseOwnership = FindPointAskPhysicalPoseOwnershipState.Owned;
            return true;
        }

        public bool TryRecordLookLogicalConfirmed(string targetKey)
        {
            if (!LookRequested || LookLogicalConfirmed || !Matches(targetKey))
                return false;
            LookLogicalConfirmed = true;
            return true;
        }

        public bool TryRecordNeckCommandDispatched(
            string targetKey,
            int servoId,
            int servoAngle,
            bool outputGateOpen)
        {
            if (!LookLogicalConfirmed || NeckCommandDispatched ||
                !Matches(targetKey) || !outputGateOpen || servoId < 0 || servoId > 1)
            {
                return false;
            }
            NeckServoId = servoId;
            NeckServoAngle = servoAngle;
            NeckCommandDispatched = true;
            NeckCompletion =
                FindPointAskPhysicalCompletionState.PhysicalCompletionUnverified;
            return true;
        }

        public bool TryRecordPointRequested(
            string targetKey,
            string activeArmId,
            float targetWorldX,
            float targetWorldY,
            float targetWorldZ)
        {
            if (!LookLogicalConfirmed || PointRequested || !Matches(targetKey) ||
                string.IsNullOrWhiteSpace(activeArmId))
            {
                return false;
            }
            PointTargetKey = Normalize(targetKey);
            PointArmId = Normalize(activeArmId);
            PointTargetWorldX = targetWorldX;
            PointTargetWorldY = targetWorldY;
            PointTargetWorldZ = targetWorldZ;
            PointRequested = true;
            return true;
        }

        public bool TryRecordPointLogicalConfirmed(string targetKey)
        {
            if (!PointRequested || PointLogicalConfirmed || !Matches(targetKey))
                return false;
            PointLogicalConfirmed = true;
            return true;
        }

        public bool TryRecordArmCommandDispatched(
            string targetKey,
            int servoId,
            int servoAngle,
            bool armOutputArmed)
        {
            if (!PointRequested || ArmCommandDispatched || !Matches(targetKey) ||
                !armOutputArmed || servoId < 2 || servoId > 9)
            {
                return false;
            }
            ArmServoId = servoId;
            ArmServoAngle = servoAngle;
            ArmCommandDispatched = true;
            ArmCompletion =
                FindPointAskPhysicalCompletionState.PhysicalCompletionUnverified;
            return true;
        }

        public bool TryRecordQuestion(string targetKey, string questionId)
        {
            if (!PointLogicalConfirmed || QuestionId.Length > 0 ||
                !Matches(targetKey) || string.IsNullOrWhiteSpace(questionId))
            {
                return false;
            }
            QuestionId = Normalize(questionId);
            QuestionTargetKey = Normalize(targetKey);
            return true;
        }

        public bool TryRecordAskRequested(string targetKey)
        {
            if (QuestionId.Length == 0 || AskRequested || !Matches(targetKey))
                return false;
            AskTargetKey = Normalize(targetKey);
            AskRequested = true;
            return true;
        }

        public bool TryRecordSpeechRequested(string runtimeSpeechId)
        {
            if (!AskRequested || SpeechRequested ||
                string.IsNullOrWhiteSpace(runtimeSpeechId))
            {
                return false;
            }
            RuntimeSpeechId = Normalize(runtimeSpeechId);
            SpeechRequested = true;
            return true;
        }

        public bool TryRecordSpeechStarted(string runtimeSpeechId)
        {
            if (!SpeechRequested || SpeechStarted || !MatchesSpeech(runtimeSpeechId))
                return false;
            SpeechStarted = true;
            return true;
        }

        public bool TryRecordQuestionSpeechCompletedAndRetain(
            string runtimeSpeechId)
        {
            if (!SpeechStarted || SpeechCompleted ||
                PoseOwnership != FindPointAskPhysicalPoseOwnershipState.Owned ||
                !LookLogicalConfirmed || !PointLogicalConfirmed ||
                !MatchesSpeech(runtimeSpeechId))
            {
                return false;
            }

            SpeechCompleted = true;
            WaitAnswerRetentionActive = true;
            return true;
        }

        public bool TryReleasePose(FindPointAskPhysicalPoseReleaseReason reason)
        {
            if (PoseOwnership == FindPointAskPhysicalPoseOwnershipState.Released)
                return false;
            PoseOwnership = FindPointAskPhysicalPoseOwnershipState.Released;
            WaitAnswerRetentionActive = false;
            PoseReleaseReason = reason;
            PoseReleaseCount++;
            return true;
        }

        public bool TryRecordTargetLost(bool holdEstablished)
        {
            FindPointAskTargetLostOutcome next = holdEstablished
                ? FindPointAskTargetLostOutcome.FrozenTargetContinuedAfterHold
                : FindPointAskTargetLostOutcome.FailedBeforeHold;
            if (TargetLostOutcome != FindPointAskTargetLostOutcome.None)
                return TargetLostOutcome == next;
            TargetLostOutcome = next;
            return true;
        }

        private bool Matches(string targetKey)
        {
            return string.Equals(
                RunTarget.TargetKey,
                Normalize(targetKey),
                StringComparison.Ordinal);
        }

        private bool MatchesSpeech(string runtimeSpeechId)
        {
            return string.Equals(
                RuntimeSpeechId,
                Normalize(runtimeSpeechId),
                StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
