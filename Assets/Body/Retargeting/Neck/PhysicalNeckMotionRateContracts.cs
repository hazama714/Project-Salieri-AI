// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    public enum PhysicalNeckMotionIntent
    {
        Tracking = 0,
        TargetLost = 1,
        ExplicitNeutral = 2,

        /// <summary>
        /// Resolver-owned semantic continuity already elapsed. This remains
        /// a loss provenance signal, not an implicit neutral command.
        /// </summary>
        AttentionContinuityExpired = 3
    }

    public enum PhysicalNeckMotionPhase
    {
        Tracking = 0,
        HoldingAfterTargetLost = 1,
        ReturningToNeutral = 2,
        ExplicitNeutralReturn = 3
    }

    /// <summary>
    /// Pure, command-side motion parameters. Defaults are deliberately slow
    /// initial calibration values; they are not measured hardware calibration.
    /// </summary>
    public sealed class PhysicalNeckMotionRateSettings
    {
        public const float InitialYawSpeedDegreesPerSecond = 15f;
        public const float InitialPitchSpeedDegreesPerSecond = 10f;
        public const float InitialReturnSpeedDegreesPerSecond = 5f;
        public const float InitialTargetLostHoldSeconds = 0.4f;

        public float MaximumYawSpeedDegreesPerSecond { get; }
        public float MaximumPitchSpeedDegreesPerSecond { get; }
        public float ReturnToNeutralSpeedDegreesPerSecond { get; }
        public float TargetLostHoldSeconds { get; }
        public bool IsCalibrated { get; }

        public PhysicalNeckMotionRateSettings(
            float maximumYawSpeedDegreesPerSecond,
            float maximumPitchSpeedDegreesPerSecond,
            float returnToNeutralSpeedDegreesPerSecond,
            float targetLostHoldSeconds,
            bool isCalibrated = false)
        {
            if (!IsFiniteNonNegative(maximumYawSpeedDegreesPerSecond) ||
                !IsFiniteNonNegative(maximumPitchSpeedDegreesPerSecond) ||
                !IsFiniteNonNegative(returnToNeutralSpeedDegreesPerSecond) ||
                !IsFiniteNonNegative(targetLostHoldSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumYawSpeedDegreesPerSecond),
                    "Neck motion rates and hold duration must be finite and non-negative.");
            }

            MaximumYawSpeedDegreesPerSecond =
                maximumYawSpeedDegreesPerSecond;
            MaximumPitchSpeedDegreesPerSecond =
                maximumPitchSpeedDegreesPerSecond;
            ReturnToNeutralSpeedDegreesPerSecond =
                returnToNeutralSpeedDegreesPerSecond;
            TargetLostHoldSeconds = targetLostHoldSeconds;
            IsCalibrated = isCalibrated;
        }

        public static PhysicalNeckMotionRateSettings CreateInitialDefaults()
        {
            return new PhysicalNeckMotionRateSettings(
                InitialYawSpeedDegreesPerSecond,
                InitialPitchSpeedDegreesPerSecond,
                InitialReturnSpeedDegreesPerSecond,
                InitialTargetLostHoldSeconds,
                isCalibrated: false);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= 0f;
        }
    }

    /// <summary>
    /// Pure Desired-to-Commanded open-loop neck motion state machine.
    /// Commanded values are software estimates and never measured physical pose.
    /// </summary>
    public sealed class PhysicalNeckMotionRateController
    {
        private readonly PhysicalNeckMotionRateSettings settings;

        public float DesiredYawDegrees { get; private set; }
        public float DesiredPitchDegrees { get; private set; }
        public float CommandedYawDegrees { get; private set; }
        public float CommandedPitchDegrees { get; private set; }
        public float NeutralYawDegrees { get; private set; }
        public float NeutralPitchDegrees { get; private set; }
        public float HoldRemainingSeconds { get; private set; }
        public float LastDeltaTimeSeconds { get; private set; }
        public PhysicalNeckMotionIntent Intent { get; private set; }
        public PhysicalNeckMotionPhase Phase { get; private set; }
        public bool HasSoftwareCommandBaseline { get; private set; }
        public float ExplicitNeutralRateScale { get; private set; } = 1f;

        // No feedback source exists. This must remain false until a separate
        // measured physical-pose contract is introduced.
        public bool StartupPhysicalAngleKnown => false;

        public PhysicalNeckMotionRateSettings Settings => settings;

        public PhysicalNeckMotionRateController(
            PhysicalNeckMotionRateSettings configuredSettings,
            float initialCommandedYawDegrees = 0f,
            float initialCommandedPitchDegrees = 0f)
        {
            settings = configuredSettings ??
                throw new ArgumentNullException(nameof(configuredSettings));
            ResetCommandedBaseline(
                initialCommandedYawDegrees,
                initialCommandedPitchDegrees);
            DesiredYawDegrees = initialCommandedYawDegrees;
            DesiredPitchDegrees = initialCommandedPitchDegrees;
            NeutralYawDegrees = 0f;
            NeutralPitchDegrees = 0f;
            Intent = PhysicalNeckMotionIntent.Tracking;
            Phase = PhysicalNeckMotionPhase.Tracking;
        }

        public void SubmitDesired(
            float desiredYawDegrees,
            float desiredPitchDegrees,
            float neutralYawDegrees,
            float neutralPitchDegrees,
            PhysicalNeckMotionIntent intent,
            float explicitNeutralRateScale = 1f)
        {
            EnsureFinite(desiredYawDegrees, nameof(desiredYawDegrees));
            EnsureFinite(desiredPitchDegrees, nameof(desiredPitchDegrees));
            EnsureFinite(neutralYawDegrees, nameof(neutralYawDegrees));
            EnsureFinite(neutralPitchDegrees, nameof(neutralPitchDegrees));
            EnsureFinite(
                explicitNeutralRateScale,
                nameof(explicitNeutralRateScale));
            if (explicitNeutralRateScale < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(explicitNeutralRateScale));
            }

            NeutralYawDegrees = neutralYawDegrees;
            NeutralPitchDegrees = neutralPitchDegrees;

            switch (intent)
            {
                case PhysicalNeckMotionIntent.Tracking:
                    ExplicitNeutralRateScale = 1f;
                    Intent = intent;
                    Phase = PhysicalNeckMotionPhase.Tracking;
                    HoldRemainingSeconds = 0f;
                    DesiredYawDegrees = desiredYawDegrees;
                    DesiredPitchDegrees = desiredPitchDegrees;
                    break;

                case PhysicalNeckMotionIntent.TargetLost:
                    ExplicitNeutralRateScale = 1f;
                    Intent = intent;
                    Phase = PhysicalNeckMotionPhase.HoldingAfterTargetLost;
                    HoldRemainingSeconds = 0f;
                    DesiredYawDegrees = CommandedYawDegrees;
                    DesiredPitchDegrees = CommandedPitchDegrees;
                    break;

                case PhysicalNeckMotionIntent.ExplicitNeutral:
                    ExplicitNeutralRateScale = explicitNeutralRateScale;
                    Intent = intent;
                    Phase = PhysicalNeckMotionPhase.ExplicitNeutralReturn;
                    HoldRemainingSeconds = 0f;
                    DesiredYawDegrees = neutralYawDegrees;
                    DesiredPitchDegrees = neutralPitchDegrees;
                    break;

                case PhysicalNeckMotionIntent.AttentionContinuityExpired:
                    ExplicitNeutralRateScale = 1f;
                    Intent = intent;
                    Phase = PhysicalNeckMotionPhase.HoldingAfterTargetLost;
                    HoldRemainingSeconds = 0f;
                    DesiredYawDegrees = CommandedYawDegrees;
                    DesiredPitchDegrees = CommandedPitchDegrees;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(intent), intent, "Unknown neck motion intent.");
            }
        }

        public void Step(float deltaTimeSeconds)
        {
            float remainingTime = Mathf.Max(0f, deltaTimeSeconds);
            LastDeltaTimeSeconds = remainingTime;

            if (!HasSoftwareCommandBaseline || remainingTime <= 0f)
                return;

            if (Phase == PhysicalNeckMotionPhase.HoldingAfterTargetLost)
            {
                // LOST = HOLD_LAST_NECK_POSE. Only a later Tracking or
                // ExplicitNeutral intent may change the commanded pose.
                return;
            }

            if (remainingTime <= 0f)
                return;

            float yawRate = Phase == PhysicalNeckMotionPhase.Tracking
                ? settings.MaximumYawSpeedDegreesPerSecond
                : settings.ReturnToNeutralSpeedDegreesPerSecond *
                    (Phase == PhysicalNeckMotionPhase.ExplicitNeutralReturn
                        ? ExplicitNeutralRateScale
                        : 1f);
            float pitchRate = Phase == PhysicalNeckMotionPhase.Tracking
                ? settings.MaximumPitchSpeedDegreesPerSecond
                : settings.ReturnToNeutralSpeedDegreesPerSecond *
                    (Phase == PhysicalNeckMotionPhase.ExplicitNeutralReturn
                        ? ExplicitNeutralRateScale
                        : 1f);

            CommandedYawDegrees = MoveBounded(
                CommandedYawDegrees,
                DesiredYawDegrees,
                yawRate,
                remainingTime);
            CommandedPitchDegrees = MoveBounded(
                CommandedPitchDegrees,
                DesiredPitchDegrees,
                pitchRate,
                remainingTime);
        }

        public void ResetCommandedBaseline(
            float commandedYawDegrees,
            float commandedPitchDegrees)
        {
            EnsureFinite(commandedYawDegrees, nameof(commandedYawDegrees));
            EnsureFinite(commandedPitchDegrees, nameof(commandedPitchDegrees));
            CommandedYawDegrees = commandedYawDegrees;
            CommandedPitchDegrees = commandedPitchDegrees;
            HasSoftwareCommandBaseline = true;
        }

        private static float MoveBounded(
            float current,
            float target,
            float maximumDegreesPerSecond,
            float deltaTimeSeconds)
        {
            float maximumDelta = maximumDegreesPerSecond * deltaTimeSeconds;
            return Mathf.MoveTowards(current, target, maximumDelta);
        }

        private static void EnsureFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
