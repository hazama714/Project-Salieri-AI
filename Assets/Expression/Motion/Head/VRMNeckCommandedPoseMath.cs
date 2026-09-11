// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Body.Retargeting.Neck;

using UnityEngine;

namespace SalieriAI.Expression.Motion
{
    /// <summary>
    /// Pure conversion from the physical/canonical Commanded neck domain back
    /// to the VRM visual direction domain. It does not claim physical feedback.
    /// </summary>
    public static class VRMNeckCommandedPoseMath
    {
        private const float Epsilon = 0.000001f;

        public static bool TryBuildVirtualDirection(
            float commandedYawDegrees,
            float commandedPitchDegrees,
            VirtualNeckRetargetProfile profile,
            out Vector3 direction)
        {
            direction = Vector3.forward;
            if (profile == null || !profile.TryValidate(out _) ||
                profile.YawGain <= Epsilon ||
                profile.PitchGain <= Epsilon)
            {
                return false;
            }

            float virtualYaw =
                ((commandedYawDegrees - profile.YawOffsetDegrees) /
                    profile.YawGain) * profile.YawSign;
            float virtualPitch =
                ((commandedPitchDegrees - profile.PitchOffsetDegrees) /
                    profile.PitchGain) * profile.PitchSign;
            if (!IsFinite(virtualYaw) || !IsFinite(virtualPitch))
                return false;

            direction = Quaternion.Euler(
                -virtualPitch,
                virtualYaw,
                0f) * Vector3.forward;
            return direction.sqrMagnitude >= Epsilon;
        }

        public static Quaternion BuildReferenceCorrection(
            Vector3 rawDirection,
            Vector3 commandedDirection)
        {
            if (rawDirection.sqrMagnitude < Epsilon ||
                commandedDirection.sqrMagnitude < Epsilon)
            {
                return Quaternion.identity;
            }

            return Quaternion.FromToRotation(
                rawDirection.normalized,
                commandedDirection.normalized);
        }

        public static void CalculateYawPitch(
            Vector3 direction,
            out float yawDegrees,
            out float pitchDegrees)
        {
            if (direction.sqrMagnitude < Epsilon)
            {
                yawDegrees = 0f;
                pitchDegrees = 0f;
                return;
            }

            direction.Normalize();
            yawDegrees = Mathf.Atan2(direction.x, direction.z) *
                Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(
                direction.x * direction.x +
                direction.z * direction.z);
            pitchDegrees = Mathf.Atan2(direction.y, horizontal) *
                Mathf.Rad2Deg;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
