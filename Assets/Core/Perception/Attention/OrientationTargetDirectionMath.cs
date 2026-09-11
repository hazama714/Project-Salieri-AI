// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// Pure body-relative direction conversion used by TargetDirection.
    /// Positions are never interpreted as directions at this boundary.
    /// </summary>
    public static class OrientationTargetDirectionMath
    {
        private const float Epsilon = 0.000001f;

        public static bool TryBuildWorldPosition(
            Vector3 anchorWorldPosition,
            Quaternion bodyWorldRotation,
            float bodyRelativeYawDegrees,
            float bodyRelativePitchDegrees,
            float distance,
            out Vector3 worldPosition)
        {
            worldPosition = anchorWorldPosition;
            if (!IsFinite(anchorWorldPosition) ||
                !IsFinite(bodyWorldRotation) ||
                !IsFinite(bodyRelativeYawDegrees) ||
                !IsFinite(bodyRelativePitchDegrees) ||
                !IsFinite(distance) ||
                distance <= 0f)
            {
                return false;
            }

            Vector3 bodyDirection = Quaternion.Euler(
                -bodyRelativePitchDegrees,
                bodyRelativeYawDegrees,
                0f) * Vector3.forward;
            Vector3 worldDirection = bodyWorldRotation * bodyDirection;
            if (worldDirection.sqrMagnitude < Epsilon)
                return false;

            worldPosition = anchorWorldPosition +
                worldDirection.normalized * distance;
            return IsFinite(worldPosition);
        }

        public static bool TryCalculateBodyRelativeAngles(
            Vector3 anchorWorldPosition,
            Vector3 targetWorldPosition,
            Quaternion bodyWorldRotation,
            out float yawDegrees,
            out float pitchDegrees)
        {
            yawDegrees = 0f;
            pitchDegrees = 0f;
            if (!IsFinite(anchorWorldPosition) ||
                !IsFinite(targetWorldPosition) ||
                !IsFinite(bodyWorldRotation))
            {
                return false;
            }

            Vector3 worldDirection = targetWorldPosition - anchorWorldPosition;
            if (worldDirection.sqrMagnitude < Epsilon)
                return false;

            Vector3 bodyDirection =
                Quaternion.Inverse(bodyWorldRotation) *
                worldDirection.normalized;
            if (bodyDirection.sqrMagnitude < Epsilon)
                return false;

            bodyDirection.Normalize();
            yawDegrees = Mathf.Atan2(
                bodyDirection.x,
                bodyDirection.z) * Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(
                bodyDirection.x * bodyDirection.x +
                bodyDirection.z * bodyDirection.z);
            pitchDegrees = Mathf.Atan2(
                bodyDirection.y,
                horizontal) * Mathf.Rad2Deg;
            return IsFinite(yawDegrees) && IsFinite(pitchDegrees);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
