// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Skills.Body.Pointing
{
    public enum PointSoftwareSettleDecision
    {
        Waiting = 0,
        Settled = 10,
        TimedOut = 20
    }

    /// <summary>
    /// POINTのread-only software facts。
    /// Physical position、Servo ACK、Encoder値は表現しない。
    /// </summary>
    public readonly struct PointSoftwareSettleObservation
    {
        public bool TargetValid { get; }
        public bool IkResultValid { get; }
        public bool IkResultFresh { get; }
        public bool IkReachedTarget { get; }
        public float HandTargetErrorMeters { get; }
        public float HandTargetToleranceMeters { get; }
        public float IkErrorMeters { get; }
        public float IkToleranceMeters { get; }
        public string WaitingReason { get; }

        public bool IsSoftwareSettled =>
            TargetValid &&
            IkResultValid &&
            IkResultFresh &&
            IkReachedTarget &&
            IsWithinTolerance(
                HandTargetErrorMeters,
                HandTargetToleranceMeters) &&
            IsWithinTolerance(IkErrorMeters, IkToleranceMeters);

        public PointSoftwareSettleObservation(
            bool targetValid,
            bool ikResultValid,
            bool ikResultFresh,
            bool ikReachedTarget,
            float handTargetErrorMeters,
            float handTargetToleranceMeters,
            float ikErrorMeters,
            float ikToleranceMeters,
            string waitingReason)
        {
            TargetValid = targetValid;
            IkResultValid = ikResultValid;
            IkResultFresh = ikResultFresh;
            IkReachedTarget = ikReachedTarget;
            HandTargetErrorMeters = handTargetErrorMeters;
            HandTargetToleranceMeters = handTargetToleranceMeters;
            IkErrorMeters = ikErrorMeters;
            IkToleranceMeters = ikToleranceMeters;
            WaitingReason = waitingReason ?? string.Empty;
        }

        private static bool IsWithinTolerance(float value, float tolerance)
        {
            return !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                !float.IsNaN(tolerance) &&
                !float.IsInfinity(tolerance) &&
                tolerance >= 0f &&
                value >= 0f &&
                value <= tolerance;
        }
    }

    /// <summary>
    /// POINT software settleの小さなlatch。
    /// TerminalとASK entry消費を分離し、ASKを一度だけ許可する。
    /// </summary>
    public sealed class PointSoftwareSettleGate
    {
        public const float DefaultTimeoutSeconds = 5f;

        public PointSoftwareSettleDecision Decision { get; private set; } =
            PointSoftwareSettleDecision.Waiting;

        public bool AskEntryConsumed { get; private set; }

        public PointSoftwareSettleDecision Evaluate(
            PointSoftwareSettleObservation observation,
            float elapsedSeconds,
            float timeoutSeconds)
        {
            if (Decision != PointSoftwareSettleDecision.Waiting)
                return Decision;

            if (observation.IsSoftwareSettled)
            {
                Decision = PointSoftwareSettleDecision.Settled;
                return Decision;
            }

            if (elapsedSeconds >= timeoutSeconds)
                Decision = PointSoftwareSettleDecision.TimedOut;

            return Decision;
        }

        public bool TryConsumeAskEntry()
        {
            if (Decision != PointSoftwareSettleDecision.Settled ||
                AskEntryConsumed)
            {
                return false;
            }

            AskEntryConsumed = true;
            return true;
        }
    }
}
