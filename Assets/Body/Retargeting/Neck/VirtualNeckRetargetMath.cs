// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>Immutable diagnostic result for every retarget stage.</summary>
    public sealed class VirtualNeckRetargetEvaluation
    {
        public float VirtualYaw { get; }
        public float VirtualPitch { get; }
        public float SignedYaw { get; }
        public float SignedPitch { get; }
        public float ScaledYaw { get; }
        public float ScaledPitch { get; }
        public float OffsetYaw { get; }
        public float OffsetPitch { get; }
        public float ClampedYaw { get; }
        public float ClampedPitch { get; }
        public float RateLimitedYaw { get; }
        public float RateLimitedPitch { get; }
        public bool Clamped { get; }
        public bool RateLimited { get; }

        internal VirtualNeckRetargetEvaluation(
            float virtualYaw,
            float virtualPitch,
            float signedYaw,
            float signedPitch,
            float scaledYaw,
            float scaledPitch,
            float offsetYaw,
            float offsetPitch,
            float clampedYaw,
            float clampedPitch,
            float rateLimitedYaw,
            float rateLimitedPitch,
            bool clamped,
            bool rateLimited)
        {
            VirtualYaw = virtualYaw;
            VirtualPitch = virtualPitch;
            SignedYaw = signedYaw;
            SignedPitch = signedPitch;
            ScaledYaw = scaledYaw;
            ScaledPitch = scaledPitch;
            OffsetYaw = offsetYaw;
            OffsetPitch = offsetPitch;
            ClampedYaw = clampedYaw;
            ClampedPitch = clampedPitch;
            RateLimitedYaw = rateLimitedYaw;
            RateLimitedPitch = rateLimitedPitch;
            Clamped = clamped;
            RateLimited = rateLimited;
        }
    }

    /// <summary>
    /// Pure solved-forward to candidate-physical-neck calculation.
    /// </summary>
    public static class VirtualNeckRetargetMath
    {
        private const float Epsilon = 0.00001f;

        public static bool TryEvaluate(
            Vector3 solvedHeadForwardInBodyFrame,
            VirtualNeckRetargetProfile profile,
            bool hasPreviousCandidate,
            float previousYaw,
            float previousPitch,
            float deltaTimeSeconds,
            out VirtualNeckRetargetEvaluation evaluation,
            out string error)
        {
            evaluation = null;
            if (profile == null)
            {
                error = "Retarget profile is missing.";
                return false;
            }
            if (!profile.TryValidate(out error))
                return false;
            if (!IsFinite(solvedHeadForwardInBodyFrame) ||
                solvedHeadForwardInBodyFrame.sqrMagnitude < Epsilon)
            {
                error = "Solved Head Forward is unavailable.";
                return false;
            }

            Vector3 forward = solvedHeadForwardInBodyFrame.normalized;
            float virtualYaw = Mathf.Atan2(forward.x, forward.z) *
                Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(
                forward.x * forward.x + forward.z * forward.z);
            float virtualPitch = Mathf.Atan2(forward.y, horizontal) *
                Mathf.Rad2Deg;

            float signedYaw = virtualYaw * profile.YawSign;
            float signedPitch = virtualPitch * profile.PitchSign;
            float scaledYaw = signedYaw * profile.YawGain;
            float scaledPitch = signedPitch * profile.PitchGain;
            float offsetYaw = scaledYaw + profile.YawOffsetDegrees;
            float offsetPitch = scaledPitch + profile.PitchOffsetDegrees;
            float clampedYaw = Mathf.Clamp(
                offsetYaw, profile.YawMinDegrees, profile.YawMaxDegrees);
            float clampedPitch = Mathf.Clamp(
                offsetPitch, profile.PitchMinDegrees, profile.PitchMaxDegrees);

            float rateLimitedYaw = RateLimit(
                clampedYaw,
                hasPreviousCandidate,
                previousYaw,
                profile.YawMaxSpeedDegreesPerSecond,
                deltaTimeSeconds);
            float rateLimitedPitch = RateLimit(
                clampedPitch,
                hasPreviousCandidate,
                previousPitch,
                profile.PitchMaxSpeedDegreesPerSecond,
                deltaTimeSeconds);

            bool clamped = !Mathf.Approximately(offsetYaw, clampedYaw) ||
                !Mathf.Approximately(offsetPitch, clampedPitch);
            bool rateLimited =
                !Mathf.Approximately(clampedYaw, rateLimitedYaw) ||
                !Mathf.Approximately(clampedPitch, rateLimitedPitch);

            evaluation = new VirtualNeckRetargetEvaluation(
                virtualYaw,
                virtualPitch,
                signedYaw,
                signedPitch,
                scaledYaw,
                scaledPitch,
                offsetYaw,
                offsetPitch,
                clampedYaw,
                clampedPitch,
                rateLimitedYaw,
                rateLimitedPitch,
                clamped,
                rateLimited);
            error = string.Empty;
            return true;
        }

        private static float RateLimit(
            float target,
            bool hasPrevious,
            float previous,
            float maximumSpeed,
            float deltaTime)
        {
            if (!hasPrevious || maximumSpeed <= 0f)
                return target;

            float maximumDelta = maximumSpeed * Mathf.Max(0f, deltaTime);
            return Mathf.MoveTowards(previous, target, maximumDelta);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
