// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.Frames
{
    /// <summary>
    /// Immutable, Transform-independent canonical body orientation.
    /// World placement and orientation relative to that placement remain
    /// explicit and compose into WorldRotation.
    /// </summary>
    public sealed class BodyOrientationState
    {
        public bool Valid { get; }
        public int CapturedFrame { get; }
        public DateTime CapturedAtUtc { get; }
        public Quaternion WorldRotation { get; }
        public Quaternion BodyPlacementRotation { get; }
        public Quaternion OrientationRelativeToPlacement { get; }
        public Vector3 Forward { get; }
        public Vector3 Up { get; }
        public Vector3 Right { get; }
        public BodyOrientationSource Source { get; }
        public BodyReferenceFrame ReferenceFrame => BodyReferenceFrame.World;
        public BodyReferenceFrame RelativeReferenceFrame =>
            BodyReferenceFrame.BodyPlacement;
        public string Version => CanonicalBodyAxes.ContractVersion;

        private BodyOrientationState(
            bool valid,
            int capturedFrame,
            DateTime capturedAtUtc,
            Quaternion worldRotation,
            Quaternion bodyPlacementRotation,
            Quaternion orientationRelativeToPlacement,
            BodyOrientationSource source)
        {
            Valid = valid;
            CapturedFrame = capturedFrame;
            CapturedAtUtc = capturedAtUtc;
            WorldRotation = worldRotation;
            BodyPlacementRotation = bodyPlacementRotation;
            OrientationRelativeToPlacement =
                orientationRelativeToPlacement;
            Forward = worldRotation * CanonicalBodyAxes.Forward;
            Up = worldRotation * CanonicalBodyAxes.Up;
            Right = worldRotation * CanonicalBodyAxes.Right;
            Source = source;
        }

        public static bool TryCreate(
            int capturedFrame,
            DateTime capturedAtUtc,
            Quaternion bodyPlacementWorldRotation,
            Quaternion orientationRelativeToPlacement,
            BodyOrientationSource source,
            out BodyOrientationState state,
            out string error)
        {
            state = null;
            error = string.Empty;
            if (capturedFrame < 0)
            {
                error = "Captured frame must be zero or greater.";
                return false;
            }

            if (capturedAtUtc.Kind != DateTimeKind.Utc)
            {
                error = "Captured time must be explicit UTC.";
                return false;
            }

            if (source == BodyOrientationSource.Unknown)
            {
                error = "Body orientation source must be explicit.";
                return false;
            }

            if (!BodyOrientationMath.TryNormalize(
                    bodyPlacementWorldRotation,
                    out Quaternion placement))
            {
                error = "Body placement rotation is invalid.";
                return false;
            }

            if (!BodyOrientationMath.TryNormalize(
                    orientationRelativeToPlacement,
                    out Quaternion relative))
            {
                error = "Orientation relative to placement is invalid.";
                return false;
            }

            if (!BodyOrientationMath.TryComposeWorldRotation(
                    placement,
                    relative,
                    out Quaternion world))
            {
                error = "Body world orientation could not be composed.";
                return false;
            }

            state = new BodyOrientationState(
                true,
                capturedFrame,
                capturedAtUtc,
                world,
                placement,
                relative,
                source);
            return true;
        }

        public static BodyOrientationState CreateInvalid(
            int capturedFrame,
            DateTime capturedAtUtc,
            BodyOrientationSource source = BodyOrientationSource.Unknown)
        {
            if (capturedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Captured time must be explicit UTC.",
                    nameof(capturedAtUtc));
            }

            return new BodyOrientationState(
                false,
                capturedFrame,
                capturedAtUtc,
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                source);
        }
    }
}
