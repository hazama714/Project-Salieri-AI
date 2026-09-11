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
    public enum LookVisualSoftwareSettleDecision
    {
        Waiting = 0,
        Tracking = 10,
        Settled = 20,
        TimedOut = 30
    }

    /// <summary>
    /// LOOK settle判定へ渡すread-only visual/software facts。
    /// Servo、encoder、ACK、Physical Position Reachedは表現しない。
    /// </summary>
    public readonly struct LookVisualSoftwareSettleObservation
    {
        public bool HasDirectObjectObservation { get; }
        public string ResolverTargetKey { get; }
        public string DriverTargetKey { get; }
        public long SourceFrameId { get; }
        public long DriverAcceptedSourceFrameId { get; }
        public float NormalizedCenterX { get; }
        public float NormalizedCenterY { get; }
        public string WaitingReason { get; }

        public LookVisualSoftwareSettleObservation(
            bool hasDirectObjectObservation,
            string resolverTargetKey,
            string driverTargetKey,
            long sourceFrameId,
            long driverAcceptedSourceFrameId,
            float normalizedCenterX,
            float normalizedCenterY,
            string waitingReason)
        {
            HasDirectObjectObservation = hasDirectObjectObservation;
            ResolverTargetKey = resolverTargetKey ?? string.Empty;
            DriverTargetKey = driverTargetKey ?? string.Empty;
            SourceFrameId = sourceFrameId;
            DriverAcceptedSourceFrameId = driverAcceptedSourceFrameId;
            NormalizedCenterX = normalizedCenterX;
            NormalizedCenterY = normalizedCenterY;
            WaitingReason = waitingReason ?? string.Empty;
        }
    }

    /// <summary>
    /// LOOK開始後のfresh direct observationが画面中心許容域へ入った事実だけを
    /// POINT entryへ変換するpure latch。Motion Smoothingは引き続き唯一の運動owner。
    /// </summary>
    public sealed class LookVisualSoftwareSettleGate
    {
        // Unity normalized viewport units. Each axis may differ by at most 0.05
        // from 0.5, so exact center equality is not required. This is one
        // behavior-level visual tolerance and is not a physical angle/pose fact.
        public const float DefaultNormalizedCenterTolerance = 0.05f;

        private readonly string requiredTargetKey;
        private readonly long lookStartedSourceFrameId;
        private long lastEvaluatedFreshSourceFrameId;

        public LookVisualSoftwareSettleDecision Decision { get; private set; } =
            LookVisualSoftwareSettleDecision.Waiting;

        public bool PointEntryConsumed { get; private set; }

        public string LastReason { get; private set; } =
            "Waiting for a post-LOOK direct observation.";

        public long LastEvaluatedFreshSourceFrameId =>
            lastEvaluatedFreshSourceFrameId;

        public LookVisualSoftwareSettleGate(
            string requiredTargetKey,
            long lookStartedSourceFrameId)
        {
            this.requiredTargetKey = requiredTargetKey ?? string.Empty;
            this.lookStartedSourceFrameId = lookStartedSourceFrameId;
        }

        public LookVisualSoftwareSettleDecision Evaluate(
            LookVisualSoftwareSettleObservation observation,
            float elapsedSeconds,
            float timeoutSeconds)
        {
            if (Decision == LookVisualSoftwareSettleDecision.Settled ||
                Decision == LookVisualSoftwareSettleDecision.TimedOut)
            {
                return Decision;
            }

            if (elapsedSeconds >= timeoutSeconds)
            {
                Decision = LookVisualSoftwareSettleDecision.TimedOut;
                LastReason = string.IsNullOrWhiteSpace(observation.WaitingReason)
                    ? "LOOK visual/software settle deadline elapsed."
                    : observation.WaitingReason;
                return Decision;
            }

            if (!observation.HasDirectObjectObservation)
            {
                Decision = LookVisualSoftwareSettleDecision.Waiting;
                LastReason = string.IsNullOrWhiteSpace(observation.WaitingReason)
                    ? "Direct Object observation is unavailable."
                    : observation.WaitingReason;
                return Decision;
            }

            if (!MatchesRequiredTarget(observation.ResolverTargetKey) ||
                !MatchesRequiredTarget(observation.DriverTargetKey))
            {
                Decision = LookVisualSoftwareSettleDecision.Waiting;
                LastReason = "Resolver/driver target does not match the frozen LOOK target.";
                return Decision;
            }

            if (observation.SourceFrameId <= lookStartedSourceFrameId)
            {
                Decision = LookVisualSoftwareSettleDecision.Waiting;
                LastReason = "Waiting for a SourceFrame newer than LOOK start.";
                return Decision;
            }

            if (observation.DriverAcceptedSourceFrameId !=
                observation.SourceFrameId)
            {
                Decision = LookVisualSoftwareSettleDecision.Waiting;
                LastReason = "Fresh visual frame has not reached the motion driver.";
                return Decision;
            }

            if (observation.SourceFrameId <= lastEvaluatedFreshSourceFrameId)
                return Decision;

            lastEvaluatedFreshSourceFrameId = observation.SourceFrameId;

            if (IsWithinCenterTolerance(
                    observation.NormalizedCenterX,
                    observation.NormalizedCenterY,
                    DefaultNormalizedCenterTolerance))
            {
                Decision = LookVisualSoftwareSettleDecision.Settled;
                LastReason = string.Empty;
                return Decision;
            }

            Decision = LookVisualSoftwareSettleDecision.Tracking;
            LastReason = "Fresh target observation remains outside center tolerance.";
            return Decision;
        }

        public bool TryConsumePointEntry()
        {
            if (Decision != LookVisualSoftwareSettleDecision.Settled ||
                PointEntryConsumed)
            {
                return false;
            }

            PointEntryConsumed = true;
            return true;
        }

        private bool MatchesRequiredTarget(string targetKey)
        {
            return !string.IsNullOrWhiteSpace(requiredTargetKey) &&
                string.Equals(
                    targetKey,
                    requiredTargetKey,
                    StringComparison.Ordinal);
        }

        private static bool IsWithinCenterTolerance(
            float centerX,
            float centerY,
            float tolerance)
        {
            return IsFiniteNormalized(centerX) &&
                IsFiniteNormalized(centerY) &&
                Math.Abs(centerX - 0.5f) <= tolerance &&
                Math.Abs(centerY - 0.5f) <= tolerance;
        }

        private static bool IsFiniteNormalized(float value)
        {
            return !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= 0f &&
                value <= 1f;
        }
    }
}
