// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Frames
{
    /// <summary>
    /// Scene-independent body-frame math. No Transform, Animator, runtime
    /// authority, retargeting, or physical output is owned here.
    /// </summary>
    public static class BodyOrientationMath
    {
        private const float Epsilon = 0.000001f;

        public static bool TryNormalize(
            Quaternion value,
            out Quaternion normalized)
        {
            normalized = Quaternion.identity;
            if (!IsFinite(value))
                return false;

            float magnitudeSquared =
                value.x * value.x +
                value.y * value.y +
                value.z * value.z +
                value.w * value.w;
            if (magnitudeSquared < Epsilon)
                return false;

            float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            normalized = new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
            return true;
        }

        public static bool TryComposeWorldRotation(
            Quaternion bodyPlacementWorldRotation,
            Quaternion orientationRelativeToPlacement,
            out Quaternion bodyOrientationWorldRotation)
        {
            bodyOrientationWorldRotation = Quaternion.identity;
            if (!TryNormalize(
                    bodyPlacementWorldRotation,
                    out Quaternion placement) ||
                !TryNormalize(
                    orientationRelativeToPlacement,
                    out Quaternion relative))
            {
                return false;
            }

            return TryNormalize(
                placement * relative,
                out bodyOrientationWorldRotation);
        }

        public static bool TryCalculateNeutralRelativeRotation(
            Quaternion neutralRotation,
            Quaternion solvedRotation,
            out Quaternion neutralRelativeRotation)
        {
            neutralRelativeRotation = Quaternion.identity;
            if (!TryNormalize(neutralRotation, out Quaternion neutral) ||
                !TryNormalize(solvedRotation, out Quaternion solved))
            {
                return false;
            }

            return TryNormalize(
                Quaternion.Inverse(neutral) * solved,
                out neutralRelativeRotation);
        }

        public static bool TryWorldDirectionToBody(
            BodyOrientationState bodyOrientation,
            Vector3 worldDirection,
            out Vector3 bodyRelativeDirection)
        {
            bodyRelativeDirection = Vector3.zero;
            if (bodyOrientation == null ||
                !bodyOrientation.Valid ||
                !TryNormalizeDirection(worldDirection, out Vector3 world))
            {
                return false;
            }

            Vector3 converted =
                Quaternion.Inverse(bodyOrientation.WorldRotation) * world;
            return TryNormalizeDirection(converted, out bodyRelativeDirection);
        }

        public static bool TryBodyDirectionToWorld(
            BodyOrientationState bodyOrientation,
            Vector3 bodyRelativeDirection,
            out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;
            if (bodyOrientation == null ||
                !bodyOrientation.Valid ||
                !TryNormalizeDirection(bodyRelativeDirection, out Vector3 body))
            {
                return false;
            }

            Vector3 converted = bodyOrientation.WorldRotation * body;
            return TryNormalizeDirection(converted, out worldDirection);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        public static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool TryNormalizeDirection(
            Vector3 value,
            out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude < Epsilon)
                return false;

            normalized = value.normalized;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

