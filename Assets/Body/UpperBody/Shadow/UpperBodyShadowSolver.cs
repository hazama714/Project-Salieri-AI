// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Body.Frames;

using UnityEngine;

namespace SalieriAI.Body.UpperBody.Shadow
{
    /// <summary>
    /// Scene-independent deterministic shadow solver. Input is already in
    /// canonical body-relative direction; the solver never accepts a world
    /// target position and never writes a Transform.
    /// </summary>
    public sealed class UpperBodyShadowSolver
    {
        public const string SolverVersion = "upper-body-shadow-solver-v0.1";

        public UpperBodyShadowSolution Solve(
            BodyOrientationState bodyOrientation,
            BodyRelativeTargetDirection targetDirection,
            string targetKey,
            int sourceFrame,
            DateTime capturedAtUtc,
            UpperBodyOrientationDistributionProfile profile,
            IReadOnlyDictionary<BodySegment, bool> availability)
        {
            string profileId = profile != null ? profile.ProfileId : string.Empty;
            string profileVersion = profile != null ? profile.Version : string.Empty;
            if (capturedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Captured time must be UTC.", nameof(capturedAtUtc));
            if (sourceFrame < 0 || bodyOrientation == null ||
                !bodyOrientation.Valid || targetDirection == null || profile == null)
            {
                return UpperBodyShadowSolution.CreateInvalid(
                    targetKey, sourceFrame, capturedAtUtc,
                    profileId, profileVersion, "Invalid canonical solver input.");
            }

            Vector3 direction = targetDirection.BodyRelativeDirection;
            if (!BodyOrientationMath.IsFinite(direction) ||
                direction.sqrMagnitude < 0.000001f)
            {
                return UpperBodyShadowSolution.CreateInvalid(
                    targetKey, sourceFrame, capturedAtUtc,
                    profileId, profileVersion, "Invalid target direction.");
            }
            direction.Normalize();

            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(
                direction.x * direction.x + direction.z * direction.z);
            float targetPitch = Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;

            float yawWeightSum = SumAvailableWeight(profile, availability, true);
            float pitchWeightSum = SumAvailableWeight(profile, availability, false);
            if (yawWeightSum <= 0f || pitchWeightSum <= 0f)
            {
                return UpperBodyShadowSolution.CreateInvalid(
                    targetKey, sourceFrame, capturedAtUtc,
                    profileId, profileVersion,
                    "No available enabled segments can distribute the target.");
            }

            var results = new UpperBodyShadowSegmentSolution[profile.Segments.Count];
            Quaternion combined = Quaternion.identity;
            for (int i = 0; i < profile.Segments.Count; i++)
            {
                UpperBodySegmentDistribution definition = profile.Segments[i];
                bool available = IsAvailable(definition.Segment, availability);
                bool participating = definition.Enabled && available;
                float rawYaw = participating
                    ? targetYaw * definition.YawWeight / yawWeightSum
                    : 0f;
                float rawPitch = participating
                    ? targetPitch * definition.PitchWeight / pitchWeightSum
                    : 0f;
                float yaw = Mathf.Clamp(
                    rawYaw, -definition.MaxYawDegrees, definition.MaxYawDegrees);
                float pitch = Mathf.Clamp(
                    rawPitch, -definition.MaxPitchDegrees, definition.MaxPitchDegrees);
                bool clamped = Mathf.Abs(rawYaw - yaw) > 0.0001f ||
                               Mathf.Abs(rawPitch - pitch) > 0.0001f;

                Quaternion candidate =
                    Quaternion.AngleAxis(yaw, CanonicalBodyAxes.Up) *
                    Quaternion.AngleAxis(-pitch, CanonicalBodyAxes.Right);
                GetReference(
                    definition.Segment,
                    IsAvailable(BodySegment.UpperChest, availability),
                    out BodyReferenceFrame referenceFrame,
                    out BodySegment referenceSegment);
                if (!BodySegmentOrientationState.TryCreate(
                        definition.Segment,
                        referenceFrame,
                        referenceSegment,
                        candidate,
                        sourceFrame,
                        capturedAtUtc,
                        out BodySegmentOrientationState state,
                        out string stateError))
                {
                    return UpperBodyShadowSolution.CreateInvalid(
                        targetKey, sourceFrame, capturedAtUtc,
                        profileId, profileVersion, stateError);
                }

                results[i] = new UpperBodyShadowSegmentSolution(
                    definition.Segment,
                    referenceFrame,
                    referenceSegment,
                    definition.Enabled,
                    available,
                    clamped,
                    yaw,
                    pitch,
                    state);
                if (participating)
                    combined = combined * state.NeutralRelativeRotation;
            }

            Vector3 reconstructed =
                (combined * CanonicalBodyAxes.Forward).normalized;
            float residual = Vector3.Angle(reconstructed, direction);
            return new UpperBodyShadowSolution(
                true,
                targetKey,
                sourceFrame,
                capturedAtUtc,
                targetDirection.RotationFromBodyForward,
                results,
                reconstructed,
                residual,
                profile.ProfileId,
                profile.Version,
                string.Empty);
        }

        private static float SumAvailableWeight(
            UpperBodyOrientationDistributionProfile profile,
            IReadOnlyDictionary<BodySegment, bool> availability,
            bool yaw)
        {
            float sum = 0f;
            for (int i = 0; i < profile.Segments.Count; i++)
            {
                UpperBodySegmentDistribution value = profile.Segments[i];
                if (value.Enabled && IsAvailable(value.Segment, availability))
                    sum += yaw ? value.YawWeight : value.PitchWeight;
            }
            return sum;
        }

        private static bool IsAvailable(
            BodySegment segment,
            IReadOnlyDictionary<BodySegment, bool> availability)
        {
            return availability != null &&
                   availability.TryGetValue(segment, out bool value) && value;
        }

        private static void GetReference(
            BodySegment segment,
            bool upperChestAvailable,
            out BodyReferenceFrame referenceFrame,
            out BodySegment referenceSegment)
        {
            referenceSegment = BodySegment.Unknown;
            switch (segment)
            {
                case BodySegment.Pelvis:
                    referenceFrame = BodyReferenceFrame.BodyOrientation;
                    return;
                case BodySegment.Spine:
                    referenceFrame = BodyReferenceFrame.ParentSegment;
                    referenceSegment = BodySegment.Pelvis;
                    return;
                case BodySegment.Chest:
                    referenceFrame = BodyReferenceFrame.ParentSegment;
                    referenceSegment = BodySegment.Spine;
                    return;
                case BodySegment.UpperChest:
                    referenceFrame = BodyReferenceFrame.ParentSegment;
                    referenceSegment = BodySegment.Chest;
                    return;
                case BodySegment.Neck:
                    referenceFrame = BodyReferenceFrame.ParentSegment;
                    referenceSegment = upperChestAvailable
                        ? BodySegment.UpperChest
                        : BodySegment.Chest;
                    return;
                case BodySegment.Head:
                    referenceFrame = BodyReferenceFrame.ParentSegment;
                    referenceSegment = BodySegment.Neck;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(segment));
            }
        }
    }
}
