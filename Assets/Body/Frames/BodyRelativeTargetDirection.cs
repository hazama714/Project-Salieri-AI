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
    /// Pure result of expressing a world target direction in the canonical
    /// body orientation frame. It is suitable for future per-segment solvers,
    /// but owns no distribution weights or thresholds.
    /// </summary>
    public sealed class BodyRelativeTargetDirection
    {
        public Vector3 WorldDirection { get; }
        public Vector3 BodyRelativeDirection { get; }
        public float ForwardComponent => BodyRelativeDirection.z;
        public float UpComponent => BodyRelativeDirection.y;
        public float RightComponent => BodyRelativeDirection.x;
        public Quaternion RotationFromBodyForward { get; }
        public BodyReferenceFrame ReferenceFrame =>
            BodyReferenceFrame.BodyOrientation;
        public string Version => CanonicalBodyAxes.ContractVersion;

        private BodyRelativeTargetDirection(
            Vector3 worldDirection,
            Vector3 bodyRelativeDirection)
        {
            WorldDirection = worldDirection;
            BodyRelativeDirection = bodyRelativeDirection;
            RotationFromBodyForward = Quaternion.FromToRotation(
                CanonicalBodyAxes.Forward,
                bodyRelativeDirection);
        }

        public static bool TryCreate(
            BodyOrientationState bodyOrientation,
            Vector3 worldTargetDirection,
            out BodyRelativeTargetDirection result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (bodyOrientation == null || !bodyOrientation.Valid)
            {
                error = "A valid body orientation is required.";
                return false;
            }

            if (!BodyOrientationMath.IsFinite(worldTargetDirection) ||
                worldTargetDirection.sqrMagnitude < 0.000001f)
            {
                error = "World target direction is invalid.";
                return false;
            }

            Vector3 normalizedWorld = worldTargetDirection.normalized;
            if (!BodyOrientationMath.TryWorldDirectionToBody(
                    bodyOrientation,
                    normalizedWorld,
                    out Vector3 bodyRelative))
            {
                error = "World target direction could not be converted.";
                return false;
            }

            result = new BodyRelativeTargetDirection(
                normalizedWorld,
                bodyRelative);
            return true;
        }
    }
}

