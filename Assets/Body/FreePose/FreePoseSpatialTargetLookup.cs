// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Body.SpatialTarget;

using UnityEngine;

namespace SalieriAI.Body.FreePose
{
    public static class FreePoseSpatialTargetIds
    {
        public const string RightNeutral = "freepose_right_neutral";
        public const string RightChest = "freepose_right_chest";
        public const string RightWaist = "freepose_right_waist";
        public const string RightFront = "freepose_right_front";
        public const string RightSide = "freepose_right_side";
        public const string RightUp = "freepose_right_up";

        public const string LeftNeutral = "freepose_left_neutral";
        public const string LeftChest = "freepose_left_chest";
        public const string LeftWaist = "freepose_left_waist";
        public const string LeftFront = "freepose_left_front";
        public const string LeftSide = "freepose_left_side";
        public const string LeftUp = "freepose_left_up";
    }

    /// <summary>
    /// Resolves a Free Pose arm directive through the existing registry.
    /// This adapter only reads the registered Transform and never writes it.
    /// </summary>
    public static class FreePoseSpatialTargetLookup
    {
        public static bool TryResolve(
            SpatialTargetRegistry registry,
            FreePoseArmDirective directive,
            SpatialTargetHandScope requiredScope,
            out Transform targetTransform)
        {
            targetTransform = null;

            if (registry == null ||
                directive.Kind !=
                    FreePoseArmDirectiveKind.SpatialTarget ||
                string.IsNullOrWhiteSpace(directive.SpatialTargetId))
            {
                return false;
            }

            if (requiredScope != SpatialTargetHandScope.RightOnly &&
                requiredScope != SpatialTargetHandScope.LeftOnly)
            {
                return false;
            }

            if (!registry.TryGetTarget(
                    directive.SpatialTargetId,
                    out SpatialTargetData target))
            {
                return false;
            }

            bool scopeAllowed =
                requiredScope == SpatialTargetHandScope.RightOnly
                    ? target.CanApplyToRightHand()
                    : target.CanApplyToLeftHand();

            if (!scopeAllowed || target.TargetTransform == null)
                return false;

            targetTransform = target.TargetTransform;
            return true;
        }
    }
}
